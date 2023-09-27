﻿namespace CodeGenerator.Constants
{
    public static class GeneratePurpose
    {
        public const string EmbedAPIDocumentation = "* Embed API documentation";
        public const string GenerateFromJira = "* Generate code using JIRA";
        public const string GenerateFromAPI = "* Generate code using API documentation URL";
        public const string GenerateFromJiraAndAPI = "* Generate code using JIRA & API documentation URL";
        public const string UpdateSolution = "* Update existing solution to use latest technology";
    }

    public static class EmbeddingPurpose
    {
        public const string ListCollection = "* List collection";
        public const string AddNewCollection = "* Add new collection";
        public const string DeleteCollection = "* Delete collection";
    }
}
