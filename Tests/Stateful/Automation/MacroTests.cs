using System;
using System.Collections.Generic;
using System.Drawing;
using UniversalDeviceToolkit.Lib.Utils;
using Moq;
using UniversalDeviceToolkit.Lib.Macro;
using UniversalDeviceToolkit.Lib.Macro.Utils;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Automation;

public class MacroEventTests
{
    [Fact]
    public void IsUndefined_ReturnsTrue_WhenSourceIsUnknown()
    {
        var e = new MacroEvent { Source = MacroSource.Unknown, Direction = MacroDirection.Down, Key = 0x61 };
        Assert.True(e.IsUndefined());
    }

    [Fact]
    public void IsUndefined_ReturnsTrue_WhenDirectionIsUnknown()
    {
        var e = new MacroEvent { Source = MacroSource.Keyboard, Direction = MacroDirection.Unknown, Key = 0x61 };
        Assert.True(e.IsUndefined());
    }

    [Fact]
    public void IsUndefined_ReturnsFalse_WhenValidKeyboardDown()
    {
        var e = new MacroEvent { Source = MacroSource.Keyboard, Direction = MacroDirection.Down, Key = 0x61 };
        Assert.False(e.IsUndefined());
    }

    [Fact]
    public void ToString_ContainsSourceAndDirectionAndKey()
    {
        var e = new MacroEvent { Source = MacroSource.Mouse, Direction = MacroDirection.Up, Key = 42, Delay = TimeSpan.FromMilliseconds(10) };
        var s = e.ToString();
        Assert.Contains("Source:Mouse", s);
        Assert.Contains("Direction: Up", s);
        Assert.Contains("Key: 42", s);
    }

    [Fact]
    public void PointAndDelay_Persist()
    {
        var pt = new Point(10, 20);
        var delay = TimeSpan.FromSeconds(1.5);
        var e = new MacroEvent { Source = MacroSource.Mouse, Direction = MacroDirection.Move, Key = 0, Point = pt, Delay = delay };
        Assert.Equal(pt, e.Point);
        Assert.Equal(delay, e.Delay);
    }
}

public class MacroIdentifierTests
{
    [Fact]
    public void Equals_ReturnsTrue_WhenSameSourceAndKey()
    {
        var a = new MacroIdentifier(MacroSource.Keyboard, 0x61);
        var b = new MacroIdentifier(MacroSource.Keyboard, 0x61);
        Assert.Equal(a, b);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenDifferentSource()
    {
        var a = new MacroIdentifier(MacroSource.Keyboard, 0x61);
        var b = new MacroIdentifier(MacroSource.Mouse, 0x61);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenDifferentKey()
    {
        var a = new MacroIdentifier(MacroSource.Keyboard, 0x61);
        var b = new MacroIdentifier(MacroSource.Keyboard, 0x62);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GetHashCode_SameForEqualIdentifiers()
    {
        var a = new MacroIdentifier(MacroSource.Keyboard, 0x61);
        var b = new MacroIdentifier(MacroSource.Keyboard, 0x61);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void OperatorEquality_AndInequality_WorksCorrectly()
    {
        var a = new MacroIdentifier(MacroSource.Keyboard, 0x61);
        var b = new MacroIdentifier(MacroSource.Keyboard, 0x61);
        var c = new MacroIdentifier(MacroSource.Mouse, 0x61);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.False(a == c);
        Assert.True(a != c);
    }

    [Fact]
    public void Equals_ReturnsFalse_WhenComparedToObjectOfDifferentType()
    {
        var a = new MacroIdentifier(MacroSource.Keyboard, 0x61);
        Assert.False(a.Equals("not an identifier"));
    }
}

public class MacroSequenceTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var seq = new MacroSequence();
        Assert.Equal(0, seq.RepeatCount);
        Assert.False(seq.IgnoreDelays);
        Assert.False(seq.InterruptOnOtherKey);
        Assert.Null(seq.Events);
    }

    [Fact]
    public void CustomValues_Persist()
    {
        var events = new[]
        {
            new MacroEvent { Source = MacroSource.Keyboard, Direction = MacroDirection.Down, Key = 0x61 },
            new MacroEvent { Source = MacroSource.Keyboard, Direction = MacroDirection.Up, Key = 0x61 }
        };
        var seq = new MacroSequence { RepeatCount = 3, IgnoreDelays = true, InterruptOnOtherKey = true, Events = events };
        Assert.Equal(3, seq.RepeatCount);
        Assert.True(seq.IgnoreDelays);
        Assert.True(seq.InterruptOnOtherKey);
        Assert.Equal(2, seq.Events!.Length);
    }
}

[Collection(TestCollections.Settings)]
public class MacroControllerCleanUpTests
{
    private static (MacroSettings settings, Mock<IMainThreadDispatcher> dispatcher) CreateSettings()
    {
        var settings = new MacroSettings();
        var dispatcher = new Mock<IMainThreadDispatcher>();
        dispatcher.Setup(d => d.Dispatch(It.IsAny<Action>())).Callback<Action>(a => a());
        return (settings, dispatcher);
    }

    [Fact]
    public void SetSequences_RemovesDownEventsWithoutMatchingUp()
    {
        var (settings, dispatcher) = CreateSettings();
        using var ctrl = new MacroController(settings, dispatcher.Object);

        var sequences = new Dictionary<MacroIdentifier, MacroSequence>
        {
            [new(MacroSource.Keyboard, 0x61)] = new MacroSequence
            {
                Events = new[]
                {
                    new MacroEvent { Source = MacroSource.Keyboard, Direction = MacroDirection.Down, Key = 0x61 },
                    // No matching Up event
                }
            }
        };

        ctrl.SetSequences(sequences);
        var result = ctrl.GetSequences();

        // Down without Up is stripped; empty sequences are removed by ClearEmptySequences
        Assert.False(result.ContainsKey(new(MacroSource.Keyboard, 0x61)));
    }

    [Fact]
    public void SetSequences_RemovesEmptySequences()
    {
        var (settings, dispatcher) = CreateSettings();
        using var ctrl = new MacroController(settings, dispatcher.Object);

        var sequences = new Dictionary<MacroIdentifier, MacroSequence>
        {
            [new(MacroSource.Keyboard, 0x61)] = new MacroSequence { Events = Array.Empty<MacroEvent>() },
            [new(MacroSource.Keyboard, 0x62)] = new MacroSequence { Events = null }
        };

        ctrl.SetSequences(sequences);
        var result = ctrl.GetSequences();

        // Empty and null-event sequences should be removed by ClearEmptySequences
        Assert.False(result.ContainsKey(new(MacroSource.Keyboard, 0x61)));
        Assert.False(result.ContainsKey(new(MacroSource.Keyboard, 0x62)));
    }

    [Fact]
    public void SetSequences_PreservesDownUpPairs()
    {
        var (settings, dispatcher) = CreateSettings();
        using var ctrl = new MacroController(settings, dispatcher.Object);

        var sequences = new Dictionary<MacroIdentifier, MacroSequence>
        {
            [new(MacroSource.Keyboard, 0x61)] = new MacroSequence
            {
                Events = new[]
                {
                    new MacroEvent { Source = MacroSource.Keyboard, Direction = MacroDirection.Down, Key = 0x61 },
                    new MacroEvent { Source = MacroSource.Keyboard, Direction = MacroDirection.Up, Key = 0x61 }
                }
            }
        };

        ctrl.SetSequences(sequences);
        var result = ctrl.GetSequences();

        var seq = result[new(MacroSource.Keyboard, 0x61)];
        Assert.Equal(2, seq.Events!.Length);
        Assert.Equal(MacroDirection.Down, seq.Events[0].Direction);
        Assert.Equal(MacroDirection.Up, seq.Events[1].Direction);
    }

    [Fact]
    public void SetSequences_PreservesWheelEvents()
    {
        var (settings, dispatcher) = CreateSettings();
        using var ctrl = new MacroController(settings, dispatcher.Object);

        var sequences = new Dictionary<MacroIdentifier, MacroSequence>
        {
            [new(MacroSource.Mouse, 0)] = new MacroSequence
            {
                Events = new[]
                {
                    new MacroEvent { Source = MacroSource.Mouse, Direction = MacroDirection.Wheel, Key = 120 },
                    new MacroEvent { Source = MacroSource.Mouse, Direction = MacroDirection.Move, Key = 0, Point = new Point(100, 200) }
                }
            }
        };

        ctrl.SetSequences(sequences);
        var result = ctrl.GetSequences();

        var seq = result[new(MacroSource.Mouse, 0)];
        Assert.Equal(2, seq.Events!.Length);
        Assert.Equal(MacroDirection.Wheel, seq.Events[0].Direction);
        Assert.Equal(MacroDirection.Move, seq.Events[1].Direction);
    }

    [Fact]
    public void SetSequences_PreservesInterleavedDownUpPairs()
    {
        var (settings, dispatcher) = CreateSettings();
        using var ctrl = new MacroController(settings, dispatcher.Object);

        var sequences = new Dictionary<MacroIdentifier, MacroSequence>
        {
            [new(MacroSource.Keyboard, 0x61)] = new MacroSequence
            {
                Events = new[]
                {
                    new MacroEvent { Source = MacroSource.Keyboard, Direction = MacroDirection.Down, Key = 0x61 },
                    new MacroEvent { Source = MacroSource.Keyboard, Direction = MacroDirection.Down, Key = 0x62 },
                    new MacroEvent { Source = MacroSource.Keyboard, Direction = MacroDirection.Up, Key = 0x61 },
                    new MacroEvent { Source = MacroSource.Keyboard, Direction = MacroDirection.Up, Key = 0x62 }
                }
            }
        };

        ctrl.SetSequences(sequences);
        var result = ctrl.GetSequences();

        var seq = result[new(MacroSource.Keyboard, 0x61)];
        // Down(0x61) has Up(0x61) after it, Down(0x62) has Up(0x62) after it
        Assert.Equal(4, seq.Events!.Length);
    }

    [Fact]
    public void GetSequences_ReturnsStoredSequences()
    {
        var (settings, dispatcher) = CreateSettings();
        using var ctrl = new MacroController(settings, dispatcher.Object);

        var sequences = new Dictionary<MacroIdentifier, MacroSequence>
        {
            [new(MacroSource.Keyboard, 0x61)] = new MacroSequence
            {
                Events = new[]
                {
                    new MacroEvent { Source = MacroSource.Keyboard, Direction = MacroDirection.Down, Key = 0x61 },
                    new MacroEvent { Source = MacroSource.Keyboard, Direction = MacroDirection.Up, Key = 0x61 }
                }
            }
        };

        ctrl.SetSequences(sequences);
        var result = ctrl.GetSequences();

        Assert.Single(result);
        Assert.True(result.ContainsKey(new(MacroSource.Keyboard, 0x61)));
    }
}

[Collection(TestCollections.Settings)]
public class MacroControllerEnabledTests
{
    [Fact]
    public void IsEnabled_DefaultsFalse()
    {
        var settings = new MacroSettings();
        var dispatcher = new Mock<IMainThreadDispatcher>();
        using var ctrl = new MacroController(settings, dispatcher.Object);
        Assert.False(ctrl.IsEnabled);
    }

    [Fact]
    public void SetEnabled_TogglesIsEnabled_True()
    {
        var settings = new MacroSettings();
        var dispatcher = new Mock<IMainThreadDispatcher>(MockBehavior.Strict);
        MacroController.HookInstallOverride = static () => true;
        try
        {
            using var ctrl = new MacroController(settings, dispatcher.Object);

            ctrl.SetEnabled(true);
            Assert.True(ctrl.IsEnabled);
            Assert.True(ctrl.IsHookActive);

            ctrl.SetEnabled(false);
            Assert.False(ctrl.IsEnabled);
            Assert.False(ctrl.IsHookActive);
        }
        finally
        {
            MacroController.HookInstallOverride = null;
        }
    }
}

public class MacroControllerAllowedKeysTests
{
    [Fact]
    public void AllowedRepeatCounts_ContainsExpectedValues()
    {
        Assert.Equal(10, MacroController.AllowedRepeatCounts.Length);
        Assert.Contains(1, MacroController.AllowedRepeatCounts);
        Assert.Contains(10, MacroController.AllowedRepeatCounts);
    }
}
