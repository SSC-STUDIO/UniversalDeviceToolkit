using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniversalDeviceToolkit.Lib.Extensions;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace UniversalDeviceToolkit.Lib;

public readonly struct GPUStatus(GPUState state, string? performanceState, IReadOnlyList<Process> processes)
{
    public GPUState State { get; } = state;
    public string? PerformanceState { get; } = performanceState;
    public IReadOnlyList<Process> Processes { get; } = processes;
    public int ProcessCount => Processes.Count;
}
