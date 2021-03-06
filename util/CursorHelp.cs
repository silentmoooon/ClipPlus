﻿using System;
using System.Runtime.InteropServices;

namespace ClipOne.util
{
    class CursorHelp
    {
        [DllImport("User32.dll")]

        private static extern IntPtr GetDC(HandleRef hWnd);



        [DllImport("User32.dll")]

        private static extern int ReleaseDC(HandleRef hWnd, HandleRef hDC);



        [DllImport("GDI32.dll")]

        private static extern int GetDeviceCaps(HandleRef hDC, int nIndex);



        private static int _dpi = 0;

        public static int DPI
        {

            get
            {

                if (_dpi == 0)
                {

                    HandleRef desktopHwnd = new HandleRef(null, IntPtr.Zero);

                    HandleRef desktopDC = new HandleRef(null, GetDC(desktopHwnd));

                    _dpi = GetDeviceCaps(desktopDC, 88 );

                    ReleaseDC(desktopHwnd, desktopDC);

                }

                return _dpi;

            }

        }
        public static double ConvertPixelsToDIPixels(int pixels)
        {

            return (double)pixels * 96 / DPI;

        }
    }
}
