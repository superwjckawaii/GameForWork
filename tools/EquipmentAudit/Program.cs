using System.Text;
using GameForWork.Core.Equipment;

EquipmentAuditResult result = EquipmentAudit.Run();
string markdown = result.RenderMarkdown();
if (args.Length == 0)
{
    Console.OutputEncoding = Encoding.UTF8;
    Console.Write(markdown);
}
else
{
    string path = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, markdown, new UTF8Encoding(false));
}
if (!result.Succeeded) throw new InvalidDataException(string.Join(Environment.NewLine, result.Failures));
