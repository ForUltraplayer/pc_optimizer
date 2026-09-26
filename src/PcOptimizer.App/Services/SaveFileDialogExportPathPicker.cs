/**
 * @file    : SaveFileDialogExportPathPicker.cs
 * @author  : rudals252
 * @brief   : Windows 저장 대화상자(SaveFileDialog)로 저장 경로를 고르는 IExportPathPicker 구현(제안 이름의 확장자로 리포트 JSON·사양 TXT·사양 PNG 필터를 고름)
 */

// 기본 패키지
using System.IO;
using Microsoft.Win32;

// 사용자 패키지
using PcOptimizer.App.Resources;

namespace PcOptimizer.App.Services;

/// <summary>
/// 저장 대화상자로 경로를 묻는 구현입니다. 덮어쓰기 전에 확인을 받습니다.
/// 제안 이름이 .txt·.png이면 사양 저장용 제목·필터를, 그 밖에는 리포트(JSON) 제목·필터를 씁니다.
/// </summary>
public sealed class SaveFileDialogExportPathPicker : IExportPathPicker
{
    private const string JSON_EXTENSION = ".json";
    private const string TEXT_EXTENSION = ".txt";
    private const string IMAGE_EXTENSION = ".png";

    /// <inheritdoc />
    public string? PickSavePath(string suggestedFileName)
    {
        var extension = Path.GetExtension(suggestedFileName);
        var (title, filter, defaultExtension) = extension.ToLowerInvariant() switch
        {
            TEXT_EXTENSION => (Strings.Spec_DialogTitle, Strings.Spec_TextFilter, TEXT_EXTENSION),
            IMAGE_EXTENSION => (Strings.Spec_DialogTitle, Strings.Spec_ImageFilter, IMAGE_EXTENSION),
            _ => (Strings.Export_DialogTitle, Strings.Export_Filter, JSON_EXTENSION),
        };
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            DefaultExt = defaultExtension,
            AddExtension = true,
            OverwritePrompt = true,
            FileName = suggestedFileName,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
