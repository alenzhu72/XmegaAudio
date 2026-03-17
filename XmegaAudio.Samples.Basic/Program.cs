using XmegaAudio.Audio;

var engine = new AudioEngine();

Console.WriteLine("Capture devices:");
foreach (var d in engine.GetCaptureDevices())
{
    Console.WriteLine($"- {d.Name} ({d.Id})");
}

Console.WriteLine();
Console.WriteLine("Render devices:");
foreach (var d in engine.GetRenderDevices())
{
    Console.WriteLine($"- {d.Name} ({d.Id})");
}

Console.WriteLine();
Console.WriteLine("This sample only lists devices. To run realtime processing, use the WPF app project.");

