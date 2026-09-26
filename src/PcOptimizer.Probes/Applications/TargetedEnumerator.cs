/**
 * @file    : TargetedEnumerator.cs
 * @author  : rudals252
 * @brief   : 공유 스캔 합계로 셀 수 없는 앱 캐시 관측 대상(패턴·제외가 있거나 스캔 루트 밖) 하나를 공유 스캔과 같은 건너뜀 기준으로 제한 시간 안에서 메타데이터만 열거하는 실행기(보호 재확인, reparse·placeholder 미진입, 앞선 대상이 센 파일·하위 폴더 제외, 파일 내용 읽기 없음)
 */

// 기본 패키지
using System.Security;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// 대상 열거기입니다. 한 스레드에서 동기적으로 실행하며 파일을 열지 않습니다.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>폴더마다 보호 루트를 다시 확인하고, 보호·placeholder·reparse 폴더에는 들어가지 않고 셉니다.</item>
/// <item>패턴에 맞고 제외되지 않은 파일만 셉니다. placeholder·reparse 파일은 건드리지 않고 셉니다.</item>
/// <item>앞선 대상이 이미 센 파일(전체 경로, 대소문자 무시)은 다시 세지 않고, 앞선 전체 대상의 폴더(하위 제외)에는 들어가지 않습니다.</item>
/// <item>항목마다 취소와 시간 예산을 확인하고, 넘기면 남은 폴더를 시간 초과로 세어 부분 집계로 끝냅니다.</item>
/// </list>
/// </remarks>
internal sealed class TargetedEnumerator(IDirectoryEntrySource source, TimeProvider time)
{
    /// <summary>
    /// 대상 하나를 열거합니다.
    /// </summary>
    /// <param name="target">관측 대상.</param>
    /// <param name="isProtected">보호 루트 확인 함수.</param>
    /// <param name="counted">이미 센 파일 전체 경로(대소문자 무시, 이 대상이 센 파일도 추가).</param>
    /// <param name="budget">이 대상의 시간 예산.</param>
    /// <param name="overallExpired">전체 예산을 넘겼는지 확인하는 함수.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>관측 결과.</returns>
    public TargetMeasurement Measure(
        ObservationTarget target, Func<string, bool> isProtected, HashSet<string> counted, TimeSpan budget, Func<bool> overallExpired, CancellationToken ct)
    {
        var start = time.GetTimestamp();
        bool IsOverBudget() => time.GetElapsedTime(start) > budget || overallExpired();

        if (isProtected(target.Directory))
        {
            return Result(target, TargetState.Protected, 0, 0, default);
        }

        switch (source.ProbeRoot(target.Directory))
        {
            case RootPresence.Missing:
            case RootPresence.NotDirectory:
                return Result(target, TargetState.Absent, 0, 0, default);
            case RootPresence.AccessDenied:
                return Result(target, TargetState.AccessDenied, 0, 0, default(SkipCounts).Increment(ScanSkipReason.AccessDenied));
            case RootPresence.ReparsePoint:
                return Result(target, TargetState.ReparsePoint, 0, 0, default(SkipCounts).Increment(ScanSkipReason.Reparse));
            case RootPresence.Error:
                return Result(target, TargetState.Error, 0, 0, default(SkipCounts).Increment(ScanSkipReason.InUse));
        }

        var carveOuts = target.CarveOuts.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var run = new Run(target, isProtected, counted, carveOuts, IsOverBudget, ct);
        run.Walk(source);
        if (run.RootFailure is { } failure && run.FileCount == 0)
        {
            var state = failure switch
            {
                ScanSkipReason.AccessDenied => TargetState.AccessDenied,
                ScanSkipReason.Timeout => TargetState.TimedOut,
                _ => TargetState.Error,
            };
            return Result(target, state, 0, 0, run.Skips);
        }

        return Result(target, run.Skips.Incomplete > 0 ? TargetState.Partial : TargetState.Observed, run.Bytes, run.FileCount, run.Skips);
    }

    /// <summary>
    /// 결과를 만든다. 파일을 셌으면 경로 단위로만 중복을 걸렀으므로 중복 가능으로 둔다.
    /// </summary>
    private static TargetMeasurement Result(ObservationTarget target, TargetState state, long bytes, long files, SkipCounts skips)
    {
        return new TargetMeasurement(target.Order, state, bytes, files, skips, DuplicatesPossible: files > 0, FromSharedScan: false);
    }

    /// <summary>
    /// 열거 한 번의 가변 상태.
    /// </summary>
    private sealed class Run(
        ObservationTarget target, Func<string, bool> isProtected, HashSet<string> counted, HashSet<string> carveOuts, Func<bool> isOverBudget, CancellationToken ct)
    {
        /// <summary>센 크기.</summary>
        public long Bytes { get; private set; }

        /// <summary>센 파일 수.</summary>
        public long FileCount { get; private set; }

        /// <summary>건너뜀.</summary>
        public SkipCounts Skips { get; private set; }

        /// <summary>대상 폴더 자체를 열거하지 못한 사유.</summary>
        public ScanSkipReason? RootFailure { get; private set; }

        /// <summary>
        /// 깊이 우선으로 열거한다.
        /// </summary>
        public void Walk(IDirectoryEntrySource source)
        {
            var stack = new Stack<string>();
            stack.Push(target.Directory);
            while (stack.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var directory = stack.Pop();
                if (isOverBudget())
                {
                    Fail(directory, ScanSkipReason.Timeout);
                    while (stack.Count > 0)
                    {
                        Fail(stack.Pop(), ScanSkipReason.Timeout);
                    }

                    return;
                }

                ListDirectory(source, directory, stack);
            }
        }

        /// <summary>
        /// 폴더 하나를 열거해 파일을 세고 들어갈 하위 폴더를 쌓는다. 받은 항목은 예산을 넘겨도 처리한다.
        /// </summary>
        private void ListDirectory(IDirectoryEntrySource source, string directory, Stack<string> stack)
        {
            var entries = new List<DirectoryEntry>();
            ScanSkipReason? failure = null;
            try
            {
                foreach (var entry in source.Enumerate(directory))
                {
                    ct.ThrowIfCancellationRequested();
                    entries.Add(entry);
                    if (isOverBudget())
                    {
                        failure = ScanSkipReason.Timeout;
                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException)
            {
                failure = ScanSkipReason.AccessDenied;
            }
            catch (IOException)
            {
                failure = ScanSkipReason.InUse;
            }

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                if (entry.IsDirectory)
                {
                    HandleDirectory(directory, entry, stack);
                }
                else
                {
                    HandleFile(directory, entry);
                }
            }

            if (failure is { } reason)
            {
                Fail(directory, reason);
            }
        }

        /// <summary>
        /// 하위 폴더: 하위 포함 대상만 들어가며, 보호·placeholder·reparse는 세고, 앞선 대상 폴더·전체 제외 폴더는 조용히 건너뛴다.
        /// </summary>
        private void HandleDirectory(string parent, DirectoryEntry entry, Stack<string> stack)
        {
            if (!target.Recurse)
            {
                return;
            }

            var child = Path.Join(parent, entry.Name);
            if (isProtected(child))
            {
                Skips = Skips.Increment(ScanSkipReason.ProtectedExcluded);
            }
            else if (EntryAttributes.IsPlaceholder(entry.Attributes))
            {
                Skips = Skips.Increment(ScanSkipReason.Placeholder);
            }
            else if (EntryAttributes.IsReparsePoint(entry.Attributes))
            {
                Skips = Skips.Increment(ScanSkipReason.Reparse);
            }
            else if (!carveOuts.Contains(child) && !ExclusionMatcher.ExcludesSubtree(target.Exclusions, child))
            {
                stack.Push(child);
            }
        }

        /// <summary>
        /// 파일: 패턴에 맞고 제외되지 않으면, placeholder·reparse는 세기만 하고, 처음 보는 경로만 크기에 더한다.
        /// </summary>
        private void HandleFile(string directory, DirectoryEntry entry)
        {
            if (!RulePatternMatcher.MatchesAny(target.Patterns, entry.Name) || ExclusionMatcher.IsExcluded(target.Exclusions, directory, entry.Name))
            {
                return;
            }

            if (EntryAttributes.IsPlaceholder(entry.Attributes))
            {
                Skips = Skips.Increment(ScanSkipReason.Placeholder);
                return;
            }

            if (EntryAttributes.IsReparsePoint(entry.Attributes))
            {
                Skips = Skips.Increment(ScanSkipReason.Reparse);
                return;
            }

            if (counted.Add(Path.Join(directory, entry.Name)))
            {
                Bytes += entry.Length;
                FileCount++;
            }
        }

        /// <summary>
        /// 폴더 하나를 끝까지 열거하지 못했음을 센다(대상 폴더 자체면 사유를 기억).
        /// </summary>
        private void Fail(string directory, ScanSkipReason reason)
        {
            Skips = Skips.Increment(reason);
            if (string.Equals(directory, target.Directory, StringComparison.OrdinalIgnoreCase))
            {
                RootFailure ??= reason;
            }
        }
    }
}
