﻿
using ClipOne.model;
using ClipOne.service;
using ClipOne.util;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Toolkit.Win32.UI.Controls.Interop.WinRT;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace ClipOne.view
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private TaskbarIcon taskbar;

        private ConfigService configService;

        private  Config config ;
        
        private ClipService clipService;
 
      
        /// <summary>
        /// 缓存目录
        /// </summary>
        public static string cacheDir = "cache";

        /// <summary>
        /// css目录
        /// </summary>
        private static readonly string CSS_DIR = "html\\css";

        /// <summary>
        /// 默认显示页面
        /// </summary>
        private static readonly string defaultHtml = "html/index.html";

       
        /// <summary>
        /// 剪切板事件
        /// </summary>
        private static readonly int WM_CLIPBOARDUPDATE = 0x031D;

        /// <summary>
        /// 注册快捷键全局原子字符串 
        /// </summary>
        private static readonly string hotkeyAtomStr = "clipOneAtom...";
        /// <summary>
        /// 快捷键全局原子
        /// </summary>
        private static int hotkeyAtom;

     

        /// <summary>
        /// 当前应用句柄
        /// </summary>
        private IntPtr wpfHwnd;
 
        public MainWindow()
        {
            InitializeComponent();
            
            Environment.CurrentDirectory =AppDomain.CurrentDomain.BaseDirectory;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            taskbar = (TaskbarIcon)FindResource("Taskbar");
            
            if (!Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }
            configService = new ConfigService();
            config = configService.GetConfig();

            clipService = new ClipService(config);

            //初始化浏览器
            InitWebView();

            //初始化托盘图标
            InitialTray();

            //注册热键,如果注册热键失败则弹出热键设置界面
            hotkeyAtom = HotKeyManager.GlobalAddAtom(hotkeyAtomStr);
        
            bool status= HotKeyManager.RegisterHotKey(wpfHwnd, hotkeyAtom, config.HotkeyModifier, config.HotkeyKey);
            
            if (!status)
            {
                Hotkey_Click(null, null);
            }

        }


        /// <summary>
        /// 添加剪切板监听， 更改窗体属性,不在alt+tab中显示
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
         
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            source.AddHook(WndProc);
            wpfHwnd = (new WindowInteropHelper(this)).Handle;
            WinAPIHelper.AddClipboardFormatListener(wpfHwnd);

            int exStyle = (int)WinAPIHelper.GetWindowLong(wpfHwnd, -20);
            exStyle |= (int)0x00000080;
            WinAPIHelper.SetWindowLong(wpfHwnd, -20, exStyle);

        }
 
        /// <summary>
        /// 初始化浏览器
        /// </summary>
        private void InitWebView()
        {
            webView1.IsJavaScriptEnabled = true;
            webView1.IsScriptNotifyAllowed = true;

            webView1.IsIndexedDBEnabled = true;
            webView1.ScriptNotify += WebView1_ScriptNotify;
            
            webView1.NavigateToLocal(defaultHtml);

            webView1.NavigationCompleted += (x, y) => {
                try {
                    webView1.InvokeScript("setMaxRecords", config.RecordCount.ToString());
                }
                catch { }
            };


        }

        

        private void WebView1_ScriptNotify(object sender, WebViewControlScriptNotifyEventArgs e)
        {

            string[] args = e.Value.Split(new char[] { '|' }, 2);

            if (args[0] == "PasteValue")
            {
               
                PasteValue(args[1]);
                

            }
            else if (args[0] == "PasteValueList")
            {


                PasteValueList(args[1]);

            }
            else if (args[0] == "DeleteImage")
            {

                new Thread(new ParameterizedThreadStart(DeleteFile)).Start(args[1]);
            }
            else if (args[0] == "ChangeWindowHeight")
            {
                ChangeWindowHeight(double.Parse(args[1]));
            }

            else if (args[0] == "clearImage")
            {
                new Thread(new ParameterizedThreadStart(ClearImage)).Start(args[1]);
            }
            else if (args[0] == "esc")
            {
                
                Hide();
            }
            else if (args[0].StartsWith("test"))
            {
                Console.WriteLine(args[1]);
            }

        }

        private void ClearImage(object images)
        {

            string[] image = JsonConvert.DeserializeObject<string[]>(HttpUtility.UrlDecode(images.ToString()));
           
            foreach (var img in Directory.GetFiles(cacheDir))
            {
               
                if (!image.Contains(img))
                {
                  
                    DeleteFile(img);
                }

            }
          

        }
        private void DeleteFile(object path)
        {
           
            for (int i = 0; i < 3; i++)
            {
                Thread.Sleep(i * 500);
                try
                {
                    string[] paths = path.ToString().Split(',');
                    foreach (string p in paths)
                    {
                        File.Delete(p);
                    }
                    return;
                }
                catch { }

            }
        }

        public void clear()
        {
            Directory.Delete(cacheDir, true);
                Directory.CreateDirectory(cacheDir);
                webView1.InvokeScript("clear");
        }

        public void refresh()
        {
            webView1.InvokeScript("saveData");
              webView1.Refresh();
        }
        /// <summary>
        /// 初始化托盘图标及菜单
        /// </summary>
        private void InitialTray()
        {

      

            //设置菜单项
            MenuItem exit = new MenuItem();
            exit.Header = "退出";
           
            MenuItem startup = new MenuItem();
            startup.Header = "开机自启";
            MenuItem hotkey = new MenuItem();
            hotkey.Header = "热键";

           
            MenuItem record = new MenuItem();
            record.Header = "记录数";
            MenuItem skin = new MenuItem();
            skin.Header = "皮肤";
            MenuItem format = new MenuItem();
            format.Header = "格式";

           
            MenuItem reload = new MenuItem();
            reload.Header = "刷新";
            MenuItem clear = new MenuItem();
            clear.Header = "清空";

            //清空记录
            clear.Click += (x, y) =>
            {
                Directory.Delete(cacheDir, true);
                Directory.CreateDirectory(cacheDir);
                webView1.InvokeScript("clear");
            };


            //刷新页面,一般用于自定义html css js时
            reload.Click += (x, y) =>
            {
                webView1.InvokeScript("saveData");
                webView1.Refresh();
            };
            //退出
            exit.Click += (x, y) => { Application.Current.Shutdown(); };

            hotkey.Click += Hotkey_Click;
            startup.Click += Startup_Click;
            startup.IsChecked = config.AutoStartup;


            //增加记录数设置子菜单项
            for (int i = 100; i <= config.MaxRecordCount; i += 100)
            {

                string recordsNum = i.ToString();
                MenuItem subRecord = new MenuItem();
                subRecord.Header = recordsNum;
                if (int.Parse(recordsNum) == config.RecordCount)
                {
                    subRecord.IsChecked = true;
                }
                subRecord.Click += RecordSet_Click;
                
                record.Items.Add(subRecord);

            }

            //增加格式选择子菜单项
            foreach (ClipType type in Enum.GetValues(typeof(ClipType)))
            {

                MenuItem subFormat = new MenuItem()
                {
                    Tag = type
                };
                subFormat.Header=(Enum.GetName(typeof(ClipType), type));
                if ((config.SupportFormat & type) != 0)
                {
                    subFormat.IsChecked = true;

                }
                if (type == ClipType.text)
                {
                    subFormat.IsEnabled = false;
                }
                else
                {
                    subFormat.Click += SubFormat_Click;
                }
                format.Items.Add(subFormat);
            }

            //根据css文件创建皮肤菜单项
            if (Directory.Exists(CSS_DIR))
            {
                string[] fileList = Directory.GetDirectories(CSS_DIR);

                foreach (string file in fileList)
                {

                    string fileName = Path.GetFileName(file);
                    MenuItem subRecord = new MenuItem();
                    subRecord.Header = fileName;
                    if (config.SkinName.Equals(fileName.ToLower()))
                    {
                        subRecord.IsChecked = true;


                    }
                    subRecord.Tag = file;
                    skin.Items.Add(subRecord);
                    subRecord.Click += SkinItem_Click;

                }
            }

            ////关联菜单项至托盘
           
            taskbar.ContextMenu.Items.Add(clear);
            taskbar.ContextMenu.Items.Add(reload);
            taskbar.ContextMenu.Items.Add(new Separator());
            taskbar.ContextMenu.Items.Add(format);
            taskbar.ContextMenu.Items.Add(skin);
            taskbar.ContextMenu.Items.Add(record);
            taskbar.ContextMenu.Items.Add(new Separator());
            taskbar.ContextMenu.Items.Add(hotkey);
            taskbar.ContextMenu.Items.Add(startup);
            taskbar.ContextMenu.Items.Add(new Separator());
            taskbar.ContextMenu.Items.Add(exit);

        }

        /// <summary>
        /// 选择支持格式
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SubFormat_Click(object sender, EventArgs e)
        {

            MenuItem item = (MenuItem)sender;
            if (item.IsChecked)
            {
                item.IsChecked = false;
                config.SupportFormat &= ~((ClipType)item.Tag);
            }
            else
            {
                item.IsChecked = true;
                config.SupportFormat |= ((ClipType)item.Tag);
            }
            configService.SaveSettings();
        }



        private void SkinItem_Click(object sender, EventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            MenuItem p = (MenuItem)item.Parent;
            foreach (MenuItem i in p.Items)
            {
                i.IsChecked = false;
            }
            item.IsChecked = true;
            config.SkinName = (string)item.Header;
            configService.SaveSettings();

            webView1.InvokeScript("saveData");
            string css = item.Tag.ToString();
            ChangeSkin(css);

        }


        /// <summary>
        /// 通过修改index.html中引入的样式文件来换肤
        /// </summary>
        /// <param name="cssPath"></param>
        private void ChangeSkin(string cssPath)
        {

            List<string> fileLines = File.ReadAllLines(defaultHtml).ToList();
            for (int i = 0; i < fileLines.Count; i++)
            {
                string str = fileLines.Last().Trim();
                if (str == "" || str.StartsWith("<link"))
                {
                    fileLines.RemoveAt(fileLines.Count - 1);
                }
                else
                {
                    break;
                }
            }
            string[] files = Directory.GetFiles(cssPath);

            foreach (string file in files)
            {

                string str = file.Replace("\\", "/").Replace("html/", "");
                fileLines.Add(" <link rel='stylesheet' type='text/css' href='" + str + "'/>");
            }
            File.WriteAllLines(defaultHtml, fileLines, Encoding.UTF8);

            webView1.Refresh();


        }

        /// <summary>
        /// 设置是否开机启动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Startup_Click(object sender, EventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            item.IsChecked = !item.IsChecked;

            configService.SetStartup(item.IsChecked);
            config.AutoStartup = item.IsChecked;
            configService.SaveSettings();
        }

        /// <summary>
        /// 设置热键
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Hotkey_Click(object sender, EventArgs e)
        {
            SetHotKeyForm sethk = new SetHotKeyForm
            {
                HotkeyKey = config.HotkeyKey,
                HotkeyModifier = config.HotkeyModifier,
                WpfHwnd = wpfHwnd,
                HotkeyAtom = hotkeyAtom
            };
            if (sethk.ShowDialog() == true)
            {

                config.HotkeyKey = sethk.HotkeyKey;
                config.HotkeyModifier = sethk.HotkeyModifier;
 
                configService.SaveSettings();
            }
        }




        /// <summary>
        /// 设置保存记录数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecordSet_Click(object sender, EventArgs e)
        {

            MenuItem item = ((MenuItem)sender);
            MenuItem p = (MenuItem)item.Parent;
            foreach (MenuItem i in p.Items)
            {
                i.IsChecked = false;
            }
            item.IsChecked = true;
            config.RecordCount = int.Parse((string)item.Header);
            configService.SaveSettings();
            webView1.InvokeScript("setMaxRecords", (String)item.Header);


        }


        /// <summary>
        /// 主要用来处理剪切板消息和热键
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <param name="handled"></param>
        /// <returns></returns>
        public IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {

            if (msg == WM_CLIPBOARDUPDATE)
            {
               

                ClipModel clip = clipService.HandClip();
                
                if (string.IsNullOrWhiteSpace(clip.ClipValue))
                {
                    handled = true;
                    return IntPtr.Zero;
                }
                
                EnQueue(clip);
                handled = true;
            }
            //触发显示界面快捷键
            else if (msg == HotKeyManager.WM_HOTKEY)
            {

                if (hotkeyAtom == wParam.ToInt32())
                {
                        
                    if (WinAPIHelper.GetCursorPos(out WinAPIHelper.POINT point))
                    {
                        double x = SystemParameters.WorkArea.Width;//得到屏幕工作区域宽度
                        double y = SystemParameters.WorkArea.Height;//得到屏幕工作区域高度
                        double mx = CursorHelp.ConvertPixelsToDIPixels(point.X);
                        double my = CursorHelp.ConvertPixelsToDIPixels(point.Y);

                        if (mx > x - ActualWidth)
                        {
                            Left = x - ActualWidth;
                        }
                        else
                        {
                            Left = mx;
                        }
                        if (my > y - ActualHeight)
                        {
                            Top = y - ActualHeight - 2;
                        }
                        else
                        {
                            Top = my - 2;
                        }
                    }
                    Show();
                    Activate();
                     
                    ShowWindowAndList();
                }
                handled = true;
            }
            
            return IntPtr.Zero;
        }

        /// <summary>
        /// 增加条目
        /// </summary>
        /// <param name="str"></param>
        private async void EnQueue(ClipModel clip)
        {
           
            string json = JsonConvert.SerializeObject(clip);
            json= HttpUtility.UrlEncode(json);
           // json = HttpUtility.UrlEncode(json);
            await webView1.InvokeScriptAsync("addData", json);
        }


        /// <summary>
        /// 显示窗口并列出所有条目
        /// </summary>
        private async void ShowWindowAndList()
        {
            await webView1.InvokeScriptAsync("showRecord");

        }

        /// <summary>
        /// 根据页面高度改变窗体高度
        /// </summary>
        /// <param name="height">页面高度</param>
        public void ChangeWindowHeight(double height)
        {
            
            Height = height + 21;
          
            double y = SystemParameters.WorkArea.Height;//得到屏幕工作区域高度
            if (ActualHeight + Top > y)
            {
                Top = y - ActualHeight - 2;
            }

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
           
            try
            {
                webView1?.InvokeScript("saveData");
                
            }
            catch { }

            webView1?.Dispose();
           
            if (wpfHwnd != null)
            {
                WinAPIHelper.RemoveClipboardFormatListener(wpfHwnd);
                HotKeyManager.UnregisterHotKey(wpfHwnd, hotkeyAtom);
                HotKeyManager.GlobalDeleteAtom(hotkeyAtomStr);

            }
           

        }

        /// <summary>
        /// 根据索引粘贴条目到活动窗口
        /// </summary>
        /// <param name="id">索引</param>
        public void PasteValue(string clipStr)
        {
            Hide();

            ClipModel clip = JsonConvert.DeserializeObject<ClipModel>(HttpUtility.UrlDecode(clipStr));

            SinglePaste(clip);



        }

        /// <summary>
        /// 粘贴条目到活动窗口 
        /// </summary>
        /// <param name="result">需要粘贴的值</param>
        /// /// <param name="neadPause">是否需要延时，单条需要，批量不需要</param>
        private void SetValueToClip(ClipModel result)
        {
            
            
            try
            {
                clipService.SetValueToClipboard(result);
                 
            }
            catch (Exception e){
                //Console.WriteLine(e.StackTrace);
                //IntPtr hwnd = WinAPIHelper.GetOpenClipboardWindow();
                //StringBuilder sb = new StringBuilder(501);
                //WinAPIHelper.GetWindowText(hwnd, sb, 500);
                //Console.WriteLine("==");
                //Console.WriteLine(sb.ToString());
                //Console.WriteLine("==");
            }
            Thread.Sleep(50);
           
            //WinAPIHelper.keybd_event((byte)KeyInterop.VirtualKeyFromKey(Key.LeftCtrl),0,0,0);//按下
            //WinAPIHelper.keybd_event((byte)KeyInterop.VirtualKeyFromKey(Key.V),0,0,0);//按下
            //WinAPIHelper.keybd_event((byte)KeyInterop.VirtualKeyFromKey(Key.V),0,2,0);//弹起
            //WinAPIHelper.keybd_event((byte)KeyInterop.VirtualKeyFromKey(Key.LeftCtrl), 0,2,0);//弹起

            KeyboardKit.Keyboard.Press(Key.LeftCtrl);
            KeyboardKit.Keyboard.Press(Key.V);

            KeyboardKit.Keyboard.Release(Key.LeftCtrl);
            KeyboardKit.Keyboard.Release(Key.V);
           


        }


        private void Window_Deactivated(object sender, EventArgs e)
        {
            
            
            
            if (Visibility == Visibility.Visible)
            {
 
                Hide();
            }
        }
 
        /// <summary>
        /// 批量粘贴
        /// </summary>
       
        public void PasteValueList(string clipListStr)
        {
            Hide();
            List<ClipModel> clipList = JsonConvert.DeserializeObject<List<ClipModel>>(HttpUtility.UrlDecode(clipListStr));
            
            BatchPaste(clipList);
        }

        /// <summary>
        /// 单个粘贴
        /// </summary>
        /// <param name="clip"></param>
        private void SinglePaste(ClipModel clip)
        {
            //设置剪切板前取消监听
            WinAPIHelper.RemoveClipboardFormatListener(wpfHwnd);
            SetValueToClip(clip);
            //设置剪切板后恢复监听
            WinAPIHelper.AddClipboardFormatListener(wpfHwnd);

        }
        /// <summary>
        /// 批量粘贴，由于循环太快、发送粘贴按键消息太慢，故延时
        /// </summary>
        /// <param name="needPause"></param>
        private void BatchPaste(List<ClipModel> clipList)
        {

            //设置剪切板前取消监听
            WinAPIHelper.RemoveClipboardFormatListener(wpfHwnd);


            for (int i = 0; i < clipList.Count; i++)
            {

                ClipModel clip = clipList[i];
                if (i != clipList.Count - 1&&!clip.ClipValue.Contains("\n"))
                {
                    clip.ClipValue += "\n";
                }
                SetValueToClip(clip);
                Thread.Sleep(100);
            }
            //设置剪切板后恢复监听
            WinAPIHelper.AddClipboardFormatListener(wpfHwnd);
        }

        
    }

}

