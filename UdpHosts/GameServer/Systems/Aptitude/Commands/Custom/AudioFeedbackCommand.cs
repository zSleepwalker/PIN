namespace GameServer.Aptitude;

public class AudioFeedbackCommand : ICommand
{
    public uint Id { get; set; }

    public AudioFeedbackCommand(uint id)
    {
        Id = id;
    }

    public bool Execute(Context context)
    {
        Serilog.Log.Information($"AudioFeedback {Id} ignored: no validated client transport is implemented yet.");
        return true;
    }

    public override string ToString()
    {
        return $"AudioFeedback (ID {Id}, unsupported client transport)";
    }
}