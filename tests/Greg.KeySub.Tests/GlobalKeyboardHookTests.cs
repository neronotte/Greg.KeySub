namespace Greg.KeySub.Tests;

public class KeyInterceptedEventArgsTests
{
    [Test]
    public void Constructor_SetsVirtualKeyCode()
    {
        var args = new KeyInterceptedEventArgs(0x41); // 'A' key
        
        Assert.That(args.VirtualKeyCode, Is.EqualTo(0x41));
    }

    [Test]
    public void Constructor_DefaultsHandledToTrue()
    {
        var args = new KeyInterceptedEventArgs(0x41);
        
        Assert.That(args.Handled, Is.True);
    }

    [Test]
    public void Constructor_DefaultsReplacementCharToBacktick()
    {
        var args = new KeyInterceptedEventArgs(0x41);
        
        Assert.That(args.ReplacementChar, Is.EqualTo('`'));
    }

    [Test]
    public void Handled_CanBeSetToFalse()
    {
        var args = new KeyInterceptedEventArgs(0x41);
        args.Handled = false;
        
        Assert.That(args.Handled, Is.False);
    }

    [Test]
    public void ReplacementChar_CanBeChanged()
    {
        var args = new KeyInterceptedEventArgs(0x41);
        args.ReplacementChar = '~';
        
        Assert.That(args.ReplacementChar, Is.EqualTo('~'));
    }
}

public class GlobalKeyboardHookTests
{
    [Test]
    public void NewInstance_CanBeCreated()
    {
        using var hook = new GlobalKeyboardHook();
        Assert.That(hook, Is.Not.Null);
    }

    [Test]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var hook = new GlobalKeyboardHook();
        
        Assert.DoesNotThrow(() =>
        {
            hook.Dispose();
            hook.Dispose();
        });
    }

    [Test]
    public void Uninstall_CanBeCalledWithoutInstall()
    {
        using var hook = new GlobalKeyboardHook();
        
        Assert.DoesNotThrow(() => hook.Uninstall());
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void Install_CreatesHook()
    {
        using var hook = new GlobalKeyboardHook();
        
        Assert.DoesNotThrow(() => hook.Install());
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void Install_CalledTwice_DoesNotThrow()
    {
        using var hook = new GlobalKeyboardHook();
        
        hook.Install();
        Assert.DoesNotThrow(() => hook.Install());
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void KeyIntercepted_EventCanBeSubscribed()
    {
        using var hook = new GlobalKeyboardHook();
        bool eventFired = false;
        
        hook.KeyIntercepted += (sender, args) => eventFired = true;
        hook.Install();
        
        // Event won't fire without actual key press, but subscription should work
        Assert.That(eventFired, Is.False);
    }
}
