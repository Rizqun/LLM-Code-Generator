using System.Diagnostics.Metrics;
using CodeGenerator.Helpers;
using CodeGenerator.Models;
using CodeGenerator.Models.Response;

namespace CodeGenerator.Services
{
    public class JiraService
    {
        public static async Task<string> GetJiraIssueDetails(JiraConfig jiraConfig, UserInput userInput)
        {
            var httpClientService = new HttpClientService($"https://{jiraConfig.Organization}/rest/api/3/issue/{userInput.JiraIssueKey}?fields=description");
            var response = httpClientService.GetWithBasicAuthTokenAsync<GetIssueResponse>(AuthHelper.GetBasicAuthTokenFromUserPass(jiraConfig.Username, jiraConfig.Key)).Result;

            var completeDescription = await ExtractTextFromContent(response.Fields.Description.Content!);

            return completeDescription;
        }

        // Method to combine all text inside Jira issue's description
        private static async Task<string> ExtractTextFromContent(List<Description> content)
        {
            List<string> textValues = new List<string>();

            foreach (var item in content)
            {
                if (item.Type == "text" && !string.IsNullOrEmpty(item.Text))
                {
                    textValues.Add(item.Text);
                }
                else if (item.Content != null)
                {
                    textValues.Add(await ExtractTextFromContent(item.Content));
                }
            }

            return string.Join("\n", textValues);
        }
    }
}
