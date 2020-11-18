namespace Miro.Services.Comments
{
    public class CommentsConsts
    {
        public const string MergeCommand = "miro merge";
        public const string CancelCommand = "miro cancel";
        public const string InfoCommand = "miro info";
        public const string WipCommand = "miro wip";
        
        public const string MiroHeader = ":dog2: <i> Miro says... </i> :dog2:";
        public const string Merging = "Merging:";
        public const string MiroMergeCheckDescription = "Write 'miro merge' to resolve this";
        public const string PullRequestCanNotBeMerged = "Ouch! Pull Request not Merged";
        public const string UpdatingAForkNotAllowed = "Sorry, Miro doesn't know how to update a fork respository yet";
        public const string TryToUpdateWithDefaultBranch = "I'll try to update branch with the default branch";
        public const string CantUpdateBranchHeader = "Damn! Can't update branch";
        public const string PrIsMergeableBody = "To merge this PR - type `miro merge`";
        public const string CantUpdateBranchBody = "This is where miro gives up, but Miro will still be listening for changes on the PR";
        public const string MiroInfoMergeNotReady = "Not ready for merging";
        public const string MiroInfoMergeReady = "PR ready for merging";
        public const string BlackListPullRequestHeader = "This Pull Request will be merged automatically by Miro";
        public const string BlackListPullRequestWipHeader = "Miro won't merge this PR since it's titled with \"WIP\"";
        public const string BlackListPullRequestBody = "No need to type `miro merge`. \n If you do *not* want this PR merged automatically, let miro know by typing `miro wip`";
        public const string BlackListPullRequestWipBody = "Type `miro merge` when you want Miro to merge it for you.";
        public const string MiroCancelHeader = "Cancelled";
        public const string MiroCancelBody = "You told miro to cancel";
        public const string MiroWipHeader = "Work in Progress, Copy that!";
        public const string MiroWipBody = "Still working on this bad boy? \n Miro will hold off merging this Pull Request. \n When you're ready, type `miro merge`";
        public const string MiroMergeCheckName = "Miro merge check";
    }
}