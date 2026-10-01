using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace AnomalyArena.EditorTools
{
    /// <summary>
    /// When tests run in the editor (Test Runner window, or a script calling TestRunnerApi), writes the results under the project's Logs/ folder:
    /// TestResults.xml (NUnit format) and TestResults.txt (a one-line summary per test).
    /// Unity only writes a result file for command-line runs; this fills that gap so scripts and tools can read the results.
    /// Running Play Mode tests reloads the domain and callbacks are lost, so InitializeOnLoad re-registers on every load.
    /// </summary>
    [InitializeOnLoad]
    internal static class TestResultsWriter
    {
        const string Dir = "Logs";
        const string XmlPath = Dir + "/TestResults.xml";
        const string SummaryPath = Dir + "/TestResults.txt";

        static TestResultsWriter()
        {
            ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Callbacks());
        }

        /// <summary>Runs AnomalyArena.Tests (Play Mode). Results are written to Logs/TestResults.*.</summary>
        [MenuItem("Anomaly Arena/4. Run Play Mode Tests")]
        public static void RunPlayModeTests()
        {
            var filter = new Filter { testMode = TestMode.PlayMode, assemblyNames = new[] { "AnomalyArena.Tests" } };
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(filter));
        }

        sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(SummaryPath, $"RUNNING {testsToRun.TestCaseCount} test(s)\n");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(XmlPath, result.ToXml().OuterXml);
                var sb = new StringBuilder();
                sb.AppendLine($"FINISHED {result.ResultState}: passed {result.PassCount}, failed {result.FailCount}, skipped {result.SkipCount}, inconclusive {result.InconclusiveCount}, {result.Duration:0.0}s");
                AppendCases(result, sb);
                File.WriteAllText(SummaryPath, sb.ToString());
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result) { }

            static void AppendCases(ITestResultAdaptor node, StringBuilder sb)
            {
                if (!node.HasChildren)
                {
                    sb.AppendLine($"{node.TestStatus,-12} {node.FullName}");
                    if (node.TestStatus == TestStatus.Failed) sb.AppendLine("    " + node.Message?.Trim().Replace("\n", "\n    "));
                    return;
                }
                foreach (var child in node.Children) AppendCases(child, sb);
            }
        }
    }
}
