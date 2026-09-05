using GameForWork.Core.Economy;

int samples = args.Length > 0 && int.TryParse(args[0], out int parsed) ? parsed : 100_000;
string? output = args.Length > 1 ? args[1] : null;
string markdown = EconomyAudit.RenderMarkdown(EconomyAudit.Run(samples));
if (string.IsNullOrWhiteSpace(output))
{
    Console.Write(markdown);
}
else
{
    string path = Path.GetFullPath(output);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, markdown);
    Console.WriteLine(path);
}
