namespace DVBSharp.Tuner.Models;

public readonly record struct TunerSnapshot(TunerInfo Info, TunerStatus? Status);
