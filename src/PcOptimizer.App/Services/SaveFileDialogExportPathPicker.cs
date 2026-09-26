/**
 * @file    : SaveFileDialogExportPathPicker.cs
 * @author  : rudals252
 * @brief   : Windows 저장 대화상자(SaveFileDialog)로 리포트 저장 경로를 고르는 IExportPathPicker 구현
 */

// 기본 패키지
using Microsoft.Win32;

// 사용자 패키지
using PcOptimizer.App.Resources;

namespace PcOptimizer.App.Services;

/// <summary>
/// 저장 대화상자로 경로를 묻는 구현입니다. 덮어쓰기 전에 확인을 받습니다.
/// </summary>
public sealed class SaveFileDialogExportPathPicker : IExportPathPicker
{
    private const string JSON_EXTENSION = ".json";

    /// <inheritdoc />
    public string? PickSavePath(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = Strings.Export_DialogTitle,
            Filter = Strings.Export_Filter,
            DefaultExt = JSON_EXTENSION,
            AddExtension = true,
            OverwritePrompt = true,
            FileName = suggestedFileName,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
