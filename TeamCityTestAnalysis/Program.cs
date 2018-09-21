using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Configuration;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Authenticators;

namespace TeamCityTestAnalysis
{
    class Program
    {
        static void Main(string[] args)
        {
            var TEAMCITY_URL = ConfigurationManager.AppSettings["TEAMCITY_URL"];
            var TEAMCITY_USERNAME = ConfigurationManager.AppSettings["TEAMCITY_USERNAME"];
            var TEAMCITY_PASSWORD = ConfigurationManager.AppSettings["TEAMCITY_PASSWORD"];

            var client = new RestClient(TEAMCITY_URL);
            var start = 0;
            var count = 100;  // TeamCity's limit
            var done = false;
            var authenticator = new HttpBasicAuthenticator(TEAMCITY_USERNAME, TEAMCITY_PASSWORD);
            var failingBuilds = new Dictionary<string, build>();
            // Collect a list of build config/branch name combinations with a failing build
            while (!done)
            {
                var request = new RestRequest("builds", Method.GET);
                request.AddQueryParameter("locator", $"status:FAILURE,branch:default:any,start:{start},count:{count}");
                request.AddQueryParameter("fields",
                    "count,nextHref,build(webUrl,buildTypeId,number,branchName,buildType(id,name,project))");
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("Accept", "application/json");
                authenticator.Authenticate(client, request);
                var response = client.Execute(request);
                var builds = JsonConvert.DeserializeObject<builds>(response.Content);
                foreach (var build in builds.build.Where(p =>
                    p.buildType.project.archived == false && IsRelevantBranchName(p.branchName)))
                {
                    var key = build.buildType.id + build.branchName;
                    if (!failingBuilds.ContainsKey(key))
                    {
                        failingBuilds.Add(key, build);
                    }
                }
                start += count;
                done = builds.count == 0;

            }

            // For each build config/branch combination, get the last build and see if it succeeded
            var file = File.CreateText(@"moo.csv");
            foreach (var build in failingBuilds.Values)
            {
                var request = new RestRequest($"buildTypes/id:{build.buildType.id}/builds/branch:name:{build.branchName}", Method.GET);
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("Accept", "application/json");
                authenticator.Authenticate(client, request);
                var response = client.Execute(request);
                var lastBuild = JsonConvert.DeserializeObject<build>(response.Content);
                if (lastBuild.status.ToLower() != "success")
                    file.WriteLine(
                        $"{build.buildType.project.name},{build.buildType.name},{build.branchName},{build.webUrl}");
            }
            file.Close();

            Console.WriteLine("\n\nPress a key...");
            Console.ReadKey();

        }

        private static bool IsRelevantBranchName(string branchName)
        {
            if (branchName == null) return false;
            return branchName == "develop" || branchName == "master" || branchName.StartsWith("support");
        }
    }

    /* TeamCity API classes */
    public class projects
    {
        public int count { get; set; }
        public string href { get; set; }
        public string nextHref { get; set; }
        public List<project> project { get; set; }
    }

    public class project
    {
        public string id { get; set; }
        public string internalId { get; set; }
        public string name { get; set; }
        public string parentProjectId { get; set; }
        public string parentProjectInternalId { get; set; }
        public string parentProjectName { get; set; }
        public bool archived { get; set; }
        public string href { get; set; }
        public string webUrl { get; set; }
        public project parentProject { get; set; }
    }

    public class testOccurrences
    {
        public int count { get; set; }
        public List<testOccurrence> testOccurrence { get; set; }
    }

    public class problemOccurrences
    {
        public int count { get; set; }
        public List<problemOccurrence> problemOccurrence { get; set; }
    }

    public class problemOccurrence
    {
        public string id { get; set; }
        public string type { get; set; }
        public string href { get; set; }
        public bool muted { get; set; }
        public string details { get; set; }
        public projects projects { get; set; }
        public build build { get; set; }
    }

    public class testOccurrence
    {
        public string name { get; set; }
        public string status { get; set; }
    }

    public class buildTypes
    {
        public int count { get; set; }
        public string href { get; set; }
        public string nextHref { get; set; }
        public List<buildType> buildType { get; set; }
    }

    public class buildType
    {
        public string id { get; set; }
        public string internalId { get; set; }
        public string name { get; set; }
        public bool paused { get; set; }
        public bool templateFlag { get; set; }
        public string projectName { get; set; }
        public string projectId { get; set; }
        public string href { get; set; }
        public string webUrl { get; set; }
        public project project { get; set; }
        public buildType template { get; set; }
        public builds builds { get; set; }
    }

    public class builds
    {
        public int count { get; set; }
        public List<build> build { get; set; }
        public string href { get; set; }
        public string nextHref { get; set; }
    }

    public class build
    {
        public string branchName { get; set; }
        public int id { get; set; }
        public string number { get; set; }
        public string status { get; set; }
        public string state { get; set; }
        public bool personal { get; set; }
        public string href { get; set; }
        public string webUrl { get; set; }
        public string statusText { get; set; }
        public buildType buildType { get; set; }
        public problemOccurrences problemOccurrences { get; set; }
    }
}
