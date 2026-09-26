using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace AnomalyArena.EditorTools
{
    /// <summary>
    /// 在编辑器里跑测试（Test Runner 窗口或脚本调用 TestRunnerApi）时，把结果写到项目的 Logs/ 下：
    /// TestResults.xml（NUnit 格式）和 TestResults.txt（一行一个测试的摘要）。
    /// Unity 只在命令行运行时才写结果文件；这里补上，方便脚本和 AI 工具读取。
    /// 跑 Play Mode 测试会重载程序域，回调会丢，所以用 InitializeOnLoad 在每次加载时重新注册。
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

        /// <summary>跑 AnomalyArena.Tests（Play Mode）。结果写到 Logs/TestResults.*。</summary>
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
