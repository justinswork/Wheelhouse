using System.Globalization;
using System.Resources;

namespace Wheelhouse.UI.Properties;

public static class Strings
{
    private static readonly ResourceManager _rm = new(
        "Wheelhouse.UI.Properties.Resources",
        typeof(Strings).Assembly);

    private static string Get(string key) =>
        _rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    // Menu strings
    public static string Menu_File => Get(nameof(Menu_File));
    public static string Menu_File_Open => Get(nameof(Menu_File_Open));
    public static string Menu_File_Clone => Get(nameof(Menu_File_Clone));
    public static string Menu_File_Init => Get(nameof(Menu_File_Init));
    public static string Menu_File_Exit => Get(nameof(Menu_File_Exit));
    public static string Menu_View => Get(nameof(Menu_View));
    public static string Menu_View_Terminal => Get(nameof(Menu_View_Terminal));
    public static string Menu_View_Reflog => Get(nameof(Menu_View_Reflog));
    public static string Menu_View_PullRequests => Get(nameof(Menu_View_PullRequests));
    public static string Menu_View_Theme => Get(nameof(Menu_View_Theme));
    public static string Menu_View_Theme_Light => Get(nameof(Menu_View_Theme_Light));
    public static string Menu_View_Theme_Dark => Get(nameof(Menu_View_Theme_Dark));
    public static string Menu_View_Theme_System => Get(nameof(Menu_View_Theme_System));
    public static string Menu_Repository => Get(nameof(Menu_Repository));
    public static string Menu_Repository_Fetch => Get(nameof(Menu_Repository_Fetch));
    public static string Menu_Repository_Pull => Get(nameof(Menu_Repository_Pull));
    public static string Menu_Repository_Push => Get(nameof(Menu_Repository_Push));
    public static string Menu_Branch => Get(nameof(Menu_Branch));
    public static string Menu_Branch_Create => Get(nameof(Menu_Branch_Create));
    public static string Menu_Branch_Stash => Get(nameof(Menu_Branch_Stash));
    public static string Menu_Tag => Get(nameof(Menu_Tag));
    public static string Menu_Tag_Create => Get(nameof(Menu_Tag_Create));
    public static string Menu_Remote => Get(nameof(Menu_Remote));
    public static string Menu_Remote_Add => Get(nameof(Menu_Remote_Add));
    public static string Menu_Remote_FetchAll => Get(nameof(Menu_Remote_FetchAll));
    public static string Menu_Tools => Get(nameof(Menu_Tools));
    public static string Menu_Tools_AccountSettings => Get(nameof(Menu_Tools_AccountSettings));
    public static string Menu_Help => Get(nameof(Menu_Help));

    // Toolbar
    public static string Toolbar_Open => Get(nameof(Toolbar_Open));
    public static string Toolbar_Fetch => Get(nameof(Toolbar_Fetch));
    public static string Toolbar_Pull => Get(nameof(Toolbar_Pull));
    public static string Toolbar_Push => Get(nameof(Toolbar_Push));
    public static string Toolbar_Branch => Get(nameof(Toolbar_Branch));
    public static string Toolbar_Stash => Get(nameof(Toolbar_Stash));
    public static string Toolbar_Open_Tooltip => Get(nameof(Toolbar_Open_Tooltip));
    public static string Toolbar_Fetch_Tooltip => Get(nameof(Toolbar_Fetch_Tooltip));
    public static string Toolbar_Pull_Tooltip => Get(nameof(Toolbar_Pull_Tooltip));
    public static string Toolbar_Push_Tooltip => Get(nameof(Toolbar_Push_Tooltip));
    public static string Toolbar_Branch_Tooltip => Get(nameof(Toolbar_Branch_Tooltip));
    public static string Toolbar_Stash_Tooltip => Get(nameof(Toolbar_Stash_Tooltip));

    // Sidebar
    public static string Sidebar_EmptyState => Get(nameof(Sidebar_EmptyState));
    public static string Sidebar_ViewReflog => Get(nameof(Sidebar_ViewReflog));
    public static string Sidebar_LocalBranches => Get(nameof(Sidebar_LocalBranches));
    public static string Sidebar_RemoteBranches => Get(nameof(Sidebar_RemoteBranches));
    public static string Sidebar_Tags => Get(nameof(Sidebar_Tags));
    public static string Sidebar_Remotes => Get(nameof(Sidebar_Remotes));
    public static string Sidebar_Stashes => Get(nameof(Sidebar_Stashes));
    public static string Sidebar_Worktrees => Get(nameof(Sidebar_Worktrees));
    public static string Sidebar_NewBranch_Tooltip => Get(nameof(Sidebar_NewBranch_Tooltip));

    // Log
    public static string Log_Title => Get(nameof(Log_Title));
    public static string Log_Refresh => Get(nameof(Log_Refresh));
    public static string Log_Filter => Get(nameof(Log_Filter));
    public static string Log_LoadMore => Get(nameof(Log_LoadMore));
    public static string Log_Col_Message => Get(nameof(Log_Col_Message));
    public static string Log_Col_Author => Get(nameof(Log_Col_Author));
    public static string Log_Col_Date => Get(nameof(Log_Col_Date));
    public static string Log_Col_SHA => Get(nameof(Log_Col_SHA));
    public static string Log_Col_CI => Get(nameof(Log_Col_CI));

    // WorkingTree
    public static string WorkingTree_Staged => Get(nameof(WorkingTree_Staged));
    public static string WorkingTree_Unstaged => Get(nameof(WorkingTree_Unstaged));
    public static string WorkingTree_NoStaged => Get(nameof(WorkingTree_NoStaged));
    public static string WorkingTree_NoUnstaged => Get(nameof(WorkingTree_NoUnstaged));
    public static string WorkingTree_Unstage_Tooltip => Get(nameof(WorkingTree_Unstage_Tooltip));
    public static string WorkingTree_Stage_Tooltip => Get(nameof(WorkingTree_Stage_Tooltip));
    public static string WorkingTree_UnstageAll => Get(nameof(WorkingTree_UnstageAll));
    public static string WorkingTree_StageAll => Get(nameof(WorkingTree_StageAll));
    public static string WorkingTree_Refresh_Tooltip => Get(nameof(WorkingTree_Refresh_Tooltip));
    public static string WorkingTree_CommitPlaceholder => Get(nameof(WorkingTree_CommitPlaceholder));
    public static string WorkingTree_Amend => Get(nameof(WorkingTree_Amend));
    public static string WorkingTree_Commit => Get(nameof(WorkingTree_Commit));
    public static string WorkingTree_Retry => Get(nameof(WorkingTree_Retry));

    // Diff
    public static string Diff_SelectFile => Get(nameof(Diff_SelectFile));
    public static string Diff_Binary => Get(nameof(Diff_Binary));
    public static string Diff_Loading => Get(nameof(Diff_Loading));
    public static string Diff_Wrap => Get(nameof(Diff_Wrap));
    public static string Diff_SideBySide => Get(nameof(Diff_SideBySide));
    public static string Diff_StageHunk => Get(nameof(Diff_StageHunk));
    public static string Diff_UnstageHunk => Get(nameof(Diff_UnstageHunk));
    public static string Diff_DiscardHunk => Get(nameof(Diff_DiscardHunk));
    public static string Diff_StageLines => Get(nameof(Diff_StageLines));
    public static string Diff_UnstageLines => Get(nameof(Diff_UnstageLines));
    public static string Diff_DiscardLines => Get(nameof(Diff_DiscardLines));

    // Tabs
    public static string Tab_WorkingTree => Get(nameof(Tab_WorkingTree));
    public static string Tab_Log => Get(nameof(Tab_Log));
    public static string Tab_Reflog => Get(nameof(Tab_Reflog));
    public static string Tab_PullRequests => Get(nameof(Tab_PullRequests));
    public static string Tab_HistoryPrefix => Get(nameof(Tab_HistoryPrefix));
    public static string Tab_BlamePrefix => Get(nameof(Tab_BlamePrefix));
    public static string Tab_IndexEditorPrefix => Get(nameof(Tab_IndexEditorPrefix));

    // Status
    public static string Status_Ready => Get(nameof(Status_Ready));
    public static string Status_Opening => Get(nameof(Status_Opening));
    public static string Status_FailedOpen => Get(nameof(Status_FailedOpen));
    public static string Status_Fetching => Get(nameof(Status_Fetching));
    public static string Status_FetchComplete => Get(nameof(Status_FetchComplete));
    public static string Status_Pulling => Get(nameof(Status_Pulling));
    public static string Status_PullComplete => Get(nameof(Status_PullComplete));
    public static string Status_Pushing => Get(nameof(Status_Pushing));
    public static string Status_PushComplete => Get(nameof(Status_PushComplete));
    public static string Status_FetchFailed => Get(nameof(Status_FetchFailed));
    public static string Status_PullFailed => Get(nameof(Status_PullFailed));
    public static string Status_PushFailed => Get(nameof(Status_PushFailed));

    // Update
    public static string Update_BannerFormat => Get(nameof(Update_BannerFormat));
    public static string Update_BannerTooltip => Get(nameof(Update_BannerTooltip));
    public static string Update_DialogTitle => Get(nameof(Update_DialogTitle));
    public static string Update_DialogHeader => Get(nameof(Update_DialogHeader));
    public static string Update_Now => Get(nameof(Update_Now));
    public static string Update_Later => Get(nameof(Update_Later));
    public static string Update_Downloading => Get(nameof(Update_Downloading));

    // Dialogs / errors
    public static string Dialog_DiscardHunk_Title => Get(nameof(Dialog_DiscardHunk_Title));
    public static string Dialog_DiscardHunk_Message => Get(nameof(Dialog_DiscardHunk_Message));
    public static string Dialog_DiscardLines_Message => Get(nameof(Dialog_DiscardLines_Message));
    public static string Dialog_Error => Get(nameof(Dialog_Error));
    public static string Dialog_Warning => Get(nameof(Dialog_Warning));
    public static string Error_OpenRepo => Get(nameof(Error_OpenRepo));
    public static string Error_CommitFailed => Get(nameof(Error_CommitFailed));
    public static string Error_CherryPickFailed => Get(nameof(Error_CherryPickFailed));
    public static string Error_RevertFailed => Get(nameof(Error_RevertFailed));
    public static string Error_ResetFailed => Get(nameof(Error_ResetFailed));

    // Branch context menu
    public static string Branch_Checkout => Get(nameof(Branch_Checkout));
    public static string Branch_MergeIntoCurrent => Get(nameof(Branch_MergeIntoCurrent));
    public static string Branch_RebaseOnto => Get(nameof(Branch_RebaseOnto));
    public static string Branch_Rename => Get(nameof(Branch_Rename));
    public static string Branch_Delete => Get(nameof(Branch_Delete));
    public static string Branch_DeleteRemote => Get(nameof(Branch_DeleteRemote));
    public static string Branch_SetUpstream => Get(nameof(Branch_SetUpstream));
    public static string Branch_FileHistory => Get(nameof(Branch_FileHistory));
    public static string Branch_Blame => Get(nameof(Branch_Blame));
    public static string WorkingTree_EditInIndex => Get(nameof(WorkingTree_EditInIndex));

    // Index editor
    public static string IndexEditor_Apply => Get(nameof(IndexEditor_Apply));
    public static string IndexEditor_Reload => Get(nameof(IndexEditor_Reload));
    public static string IndexEditor_Loading => Get(nameof(IndexEditor_Loading));
    public static string IndexEditor_Modified => Get(nameof(IndexEditor_Modified));
    public static string IndexEditor_TakeLeft => Get(nameof(IndexEditor_TakeLeft));
    public static string IndexEditor_TakeRight => Get(nameof(IndexEditor_TakeRight));
    public static string IndexEditor_PrevChange => Get(nameof(IndexEditor_PrevChange));
    public static string IndexEditor_NextChange => Get(nameof(IndexEditor_NextChange));
    public static string IndexEditor_Header_Head => Get(nameof(IndexEditor_Header_Head));
    public static string IndexEditor_Header_Index => Get(nameof(IndexEditor_Header_Index));
    public static string IndexEditor_Header_WorkingTree => Get(nameof(IndexEditor_Header_WorkingTree));
    public static string IndexEditor_Status_NoChanges => Get(nameof(IndexEditor_Status_NoChanges));
    public static string IndexEditor_Status_Format => Get(nameof(IndexEditor_Status_Format));
    public static string IndexEditor_Confirm_Discard_Title => Get(nameof(IndexEditor_Confirm_Discard_Title));
    public static string IndexEditor_Confirm_Discard_Message => Get(nameof(IndexEditor_Confirm_Discard_Message));
    public static string IndexEditor_HeadEmpty_Watermark => Get(nameof(IndexEditor_HeadEmpty_Watermark));
    public static string IndexEditor_WtEmpty_Watermark => Get(nameof(IndexEditor_WtEmpty_Watermark));
    public static string IndexEditor_TakeLeft_Tooltip => Get(nameof(IndexEditor_TakeLeft_Tooltip));
    public static string IndexEditor_TakeRight_Tooltip => Get(nameof(IndexEditor_TakeRight_Tooltip));
    public static string IndexEditor_PrevChange_Tooltip => Get(nameof(IndexEditor_PrevChange_Tooltip));
    public static string IndexEditor_NextChange_Tooltip => Get(nameof(IndexEditor_NextChange_Tooltip));
    public static string IndexEditor_Apply_Tooltip => Get(nameof(IndexEditor_Apply_Tooltip));
    public static string IndexEditor_Reload_Tooltip => Get(nameof(IndexEditor_Reload_Tooltip));

    // Relative dates
    public static string Date_JustNow => Get(nameof(Date_JustNow));
    public static string Date_MinutesAgo => Get(nameof(Date_MinutesAgo));
    public static string Date_HoursAgo => Get(nameof(Date_HoursAgo));
    public static string Date_DaysAgo => Get(nameof(Date_DaysAgo));
    public static string Date_MonthsAgo => Get(nameof(Date_MonthsAgo));
    public static string Date_YearsAgo => Get(nameof(Date_YearsAgo));

    // Log context menu
    public static string Log_CherryPick => Get(nameof(Log_CherryPick));
    public static string Log_Revert => Get(nameof(Log_Revert));
    public static string Log_ResetToHere => Get(nameof(Log_ResetToHere));

    // Tags
    public static string Tag_Push => Get(nameof(Tag_Push));
    public static string Tag_Delete => Get(nameof(Tag_Delete));

    // Stashes
    public static string Stash_Apply => Get(nameof(Stash_Apply));
    public static string Stash_PopApply => Get(nameof(Stash_PopApply));
    public static string Stash_Drop => Get(nameof(Stash_Drop));

    // Remotes
    public static string Remote_Fetch => Get(nameof(Remote_Fetch));
    public static string Remote_Prune => Get(nameof(Remote_Prune));
    public static string Remote_Rename => Get(nameof(Remote_Rename));
    public static string Remote_Remove => Get(nameof(Remote_Remove));

    // Additional sidebar tooltips
    public static string Sidebar_NewTag_Tooltip => Get(nameof(Sidebar_NewTag_Tooltip));
    public static string Sidebar_AddRemote_Tooltip => Get(nameof(Sidebar_AddRemote_Tooltip));
    public static string Sidebar_PruneWorktrees_Tooltip => Get(nameof(Sidebar_PruneWorktrees_Tooltip));
    public static string Sidebar_AddWorktree_Tooltip => Get(nameof(Sidebar_AddWorktree_Tooltip));

    // Worktrees
    public static string Worktree_Remove => Get(nameof(Worktree_Remove));
}
