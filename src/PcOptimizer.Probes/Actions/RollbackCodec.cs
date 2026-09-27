/**
 * @file    : RollbackCodec.cs
 * @author  : rudals252
 * @brief   : 복구 JSON 상한·엄격 스키마·상태 전이·불변 원문 검증
 */
using System.Text.Json;
using System.Text.Json.Serialization;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.Probes.Actions;

internal static class RollbackCodec
{
    internal const int MaxBytes = 128 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        IgnoreReadOnlyProperties = true,
    };
    internal static byte[] Encode(RollbackRecord record, string sid)
    {
        Validate(record, sid);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, Options);
        if (bytes.Length > MaxBytes) { throw new InvalidDataException("RecordTooLarge"); }
        return bytes;
    }
    internal static RollbackRecord Decode(byte[] bytes, string sid)
    {
        if (bytes.Length > MaxBytes) { throw new InvalidDataException("RecordTooLarge"); }
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
        CheckUnique(document.RootElement);
        CheckShape(document.RootElement, ["Version", "Id", "Sid", "ActionId", "Scope", "TargetKey", "Purpose", "Before", "Applied", "CreatedAt", "UpdatedAt", "State", "Revision"]);
        CheckShape(document.RootElement.GetProperty("Before"), ["Exists", "NativeType", "Data"]);
        CheckShape(document.RootElement.GetProperty("Applied"), ["Exists", "NativeType", "Data"]);
        var record = JsonSerializer.Deserialize<RollbackRecord>(bytes, Options) ?? throw new InvalidDataException("InvalidRecord");
        Validate(record, sid);
        return record;
    }
    private static void CheckShape(JsonElement element, string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal)))
        { throw new InvalidDataException("InvalidShape"); }
    }
    private static void CheckUnique(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) { return; }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Add(property.Name)) { throw new InvalidDataException("DuplicateProperty"); }
            CheckUnique(property.Value);
        }
    }
    internal static void Validate(RollbackRecord r, string sid)
    {
        if (r.Version != 1 || r.Id == Guid.Empty || r.Sid != sid || string.IsNullOrWhiteSpace(sid) || sid.Length > 184
            || r.ActionId is not (ActionId.Startup or ActionId.MachineStartup or ActionId.Power or ActionId.Display or ActionId.SystemFiles or ActionId.UpdateServices or ActionId.StartupFolder or ActionId.CommonStartupFolder or ActionId.StartupApproval or ActionId.MachineStartupApproval or ActionId.Mpo or ActionId.Hags or ActionId.GameMode or ActionId.NvidiaRebar or ActionId.NvidiaVideo or ActionId.AmdVideo)
            || !Enum.IsDefined(r.Scope) || !Enum.IsDefined(r.State) || !Enum.IsDefined(r.Purpose)
            || r.Revision < 0 || r.CreatedAt == default || r.UpdatedAt < r.CreatedAt
            || string.IsNullOrWhiteSpace(r.TargetKey) || r.TargetKey.Length > 512 || r.TargetKey.Any(char.IsControl)
            || (r.ActionId is ActionId.Power or ActionId.Display or ActionId.StartupFolder or ActionId.StartupApproval or ActionId.GameMode or ActionId.NvidiaRebar or ActionId.NvidiaVideo or ActionId.AmdVideo && r.Scope != ActionScope.CurrentUser)
            || (r.ActionId is ActionId.MachineStartup or ActionId.CommonStartupFolder or ActionId.MachineStartupApproval or ActionId.Mpo or ActionId.Hags && r.Scope != ActionScope.System)
            || (r.Purpose == RollbackPurpose.ServiceRecovery) != (r.ActionId is ActionId.SystemFiles or ActionId.UpdateServices)
            || (r.Purpose == RollbackPurpose.ServiceRecovery && r.Scope != ActionScope.System)
            || (r.Purpose == RollbackPurpose.TemporaryDisplay && r.ActionId != ActionId.Display))
        { throw new InvalidDataException("InvalidRecord"); }
        ValidateValue(r.Before);
        ValidateValue(r.Applied);
    }
    private static void ValidateValue(RollbackValue value)
    {
        if (value is null || value.Data is null || value.Data.Length > 32 * 1024 || value.NativeType is < 0 or > ushort.MaxValue
            || (!value.Exists && (value.NativeType != 0 || value.Data.Length != 0)))
        { throw new InvalidDataException("InvalidValue"); }
    }
    internal static void CheckTransition(RollbackRecord? old, RollbackRecord next)
    {
        if (old is null)
        {
            if (next.State != RollbackState.Pending || next.Revision != 0 || next.UpdatedAt != next.CreatedAt)
            { throw new InvalidDataException("PendingRequired"); }
            return;
        }
        if (next.Revision != checked(old.Revision + 1) || next.UpdatedAt < old.UpdatedAt
            || next.Id != old.Id || next.Version != old.Version || next.Sid != old.Sid || next.ActionId != old.ActionId
            || next.Scope != old.Scope || next.TargetKey != old.TargetKey || next.Purpose != old.Purpose
            || next.CreatedAt != old.CreatedAt || !next.Before.SameAs(old.Before) || !next.Applied.SameAs(old.Applied)
            || !((old.State, next.State) switch
            {
                (RollbackState.Pending, RollbackState.Applied or RollbackState.Restoring or RollbackState.Unchanged) => true,
                (RollbackState.Applied, RollbackState.Restoring or RollbackState.Unchanged) => true,
                (RollbackState.Restoring, RollbackState.Restored or RollbackState.Unchanged) => true,
                _ => false,
            }))
        { throw new InvalidDataException("InvalidTransition"); }
    }
}
