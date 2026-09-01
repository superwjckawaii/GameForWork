using System.Text;
using GameForWork.Core.P30;

IReadOnlyList<P30BuildAuditResult> results = P30BuildAudit.Run();
IReadOnlyList<string> failures = P30BuildAudit.Validate(results);
string markdown = P30BuildAudit.RenderMarkdown(results);
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
