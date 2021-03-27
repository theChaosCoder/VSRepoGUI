﻿using Jot;
using System.Windows;

namespace VSRepoGUI
{
    static class SettingsService
    {
        public static Tracker Tracker = new Tracker();

        static SettingsService()
        {
            Tracker
                .Configure<Window>()
                .Id(w => "vsrepogui_settings", null, false)
                .Properties(w => new { w.Top, w.Width, w.Height, w.Left, w.WindowState })
                .PersistOn(nameof(Window.Closing))
                .StopTrackingOn(nameof(Window.Closing));
        }
    }
}
