using System;
using System.Collections.Generic;
using System.Text;

namespace GravityTurn.Window
{
    public class WindowManager
    {
        List<BaseWindow> Windows = new List<BaseWindow>();

        public void Register(BaseWindow window)
        {
            Windows.Add(window);
        }

        public void DrawGuis()
        {
            foreach (BaseWindow window in Windows)
            {
                window.drawGUI();
            }
        }
        public void OnDestroy()
        {
            foreach (BaseWindow window in Windows)
            {
                window.OnDestroy();
            }
        }
    }
}
