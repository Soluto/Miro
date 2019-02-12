using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Miro.Models.Github.RequestPayloads;
using Miro.Services.Comments;
using Miro.Services.Logger;
using Newtonsoft.Json;
using Serilog;

namespace Miro.Services.Github
{
    public class CommentCreator
    {
        private readonly GithubHttpClient githubHttpClient;
        private readonly ILogger logger = Log.ForContext<CommentCreator>();

        public CommentCreator(GithubHttpClient githubHttpClient)
        {
            this.githubHttpClient = githubHttpClient;
        }
        
        public async Task CreateComment(string owner, string repo, int prId, string commentHeader, params string[] commentBody)
        {
            logger.WithExtraData(new { owner, repo, prId, commentHeader }).Information($"Creating comment");
            var prettyComment = BuildMarkdownComment(commentHeader, commentBody);
            await GithubRequest(owner, repo, prId, prettyComment);
        }


        public async Task CreateListedComment(string owner, string repo, int prId, string commentHeader, List<string> commentList = null)
        {
            logger.WithExtraData(new {owner, repo, prId, commentHeader}).Information($"Creating comment");
            var prettyComment = BuildMarkdownListedComment(commentHeader, commentList);
            await GithubRequest(owner, repo, prId, prettyComment);
        }

         private string BuildMarkdownComment(string header, params string[] commentBody)
        {
             var stringBuilder = MarkdownHeader(header);

            if (commentBody != null && commentBody.Any())	
            {	
               foreach (var line in commentBody)
                {
                    stringBuilder
                    .AppendLine("")
                    .AppendLine(line);
                }
            }	
            return stringBuilder.ToString();
        }

        private string BuildMarkdownListedComment(string header, List<string> commentBody)
        {
             var stringBuilder = MarkdownHeader(header);

            if (commentBody != null && commentBody.Any())
            {
                commentBody.ForEach(e => stringBuilder.AppendLine($"- #### {e}"));
            }
            return stringBuilder.ToString();
        }

        private async Task GithubRequest(string owner, string repo, int prId, string prettyComment)
        {
            var url = $"/repos/{owner}/{repo}/issues/{prId}/comments";
            var response = await githubHttpClient.Post(url, new CreateCommentPayload { Body = prettyComment });
            response.EnsureSuccessStatusCode();
        }

        private static StringBuilder MarkdownHeader(string header)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(CommentsConsts.MiroHeader)
            .AppendLine("")
            .AppendLine($"## {header}")
            .AppendLine("");
            return stringBuilder;
        }
    }
}