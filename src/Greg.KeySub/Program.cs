namespace Greg.KeySub;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        
        // Ensure only one instance runs
        using var mutex = new Mutex(true, "Greg.KeySub.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Greg.KeySub is already running.", "KeySub", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.Run(new TrayApplicationContext());
    }
}
