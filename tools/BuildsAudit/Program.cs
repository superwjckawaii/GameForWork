using System.Text;
using GameForWork.Core.Builds;

IReadOnlyList<BuildAuditResult> results = BuildAudit.Run();
IReadOnlyList<string> failures = BuildAudit.Validate(results);
string markdown = BuildAudit.RenderMarkdown(results);
if (args.Length > 0)
{
    string path = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, markdown, new UTF8Encoding(false));
}
else
{
    Console.OutputEncoding = Encoding.UTF8;
    Console.Write(markdown);
}
if (failures.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, failures));
