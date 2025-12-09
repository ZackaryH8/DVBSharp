namespace DVBSharp.Tuner;

public interface ITunerProvider
{
    IEnumerable<ITuner> CreateTuners();
}
