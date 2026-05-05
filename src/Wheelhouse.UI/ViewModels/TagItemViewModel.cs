using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows;
using Wheelhouse.Core.Models;
using Wheelhouse.Core.Services;
using Wheelhouse.UI.Messages;

namespace Wheelhouse.UI.ViewModels;

public sealed partial class TagItemViewModel : ViewModelBase
{
    private readonly IRepositoryService _repositoryService;

    public TagInfo Tag { get; }
    public string Name => Tag.Name;
    public string ShortSha => Tag.Sha.Length >= 7 ? Tag.Sha[..7] : Tag.Sha;
    public bool IsAnnotated => Tag.IsAnnotated;
    public string TypeLabel => Tag.IsAnnotated ? "annotated" : "lightweight";
    public string RelativeDate => Tag.When.HasValue ? FormatRelativeDate(Tag.When.Value) : string.Empty;

    public TagItemViewModel(TagInfo tag, IRepositoryService repositoryService)
    {
        Tag = tag;
        _repositoryService = repositoryService;
    }

    [RelayCommand]
    private async Task PushAsync()
    {
        try
        {
            await _repositoryService.PushTagAsync(Tag.Name);
            MessageBox.Show($"Tag '{Name}' pushed.", "Push Tag", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Push failed: {ex.Message}", "Push Tag", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (MessageBox.Show($"Delete tag '{Name}'?", "Delete Tag", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            await _repositoryService.DeleteTagAsync(Tag.Name);
            WeakReferenceMessenger.Default.Send(new TagChangedMessage());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Delete failed: {ex.Message}", "Delete Tag", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string FormatRelativeDate(DateTimeOffset when)
    {
        var diff = DateTimeOffset.Now - when;
        return diff.TotalSeconds < 60   ? "just now"
             : diff.TotalMinutes < 60   ? $"{(int)diff.TotalMinutes}m ago"
             : diff.TotalHours < 24     ? $"{(int)diff.TotalHours}h ago"
             : diff.TotalDays < 30      ? $"{(int)diff.TotalDays}d ago"
             : diff.TotalDays < 365     ? $"{(int)(diff.TotalDays / 30)}mo ago"
             :                            $"{(int)(diff.TotalDays / 365)}y ago";
    }
}
