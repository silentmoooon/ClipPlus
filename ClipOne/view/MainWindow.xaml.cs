﻿using CefSharp;
using CefSharp.Wpf;
using ClipOne.model;

using ClipOne.util;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using ClipOne.service;
using static ClipOne.service.ClipService;
using HtmlAgilityPack;
using System.Windows.Resources;
using System.Windows.Controls;

namespace ClipOne.view
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {

        /// <summary>
        /// 持久化路径
        /// </summary>
        private static string storePath = "store\\clip.json";

        /// <summary>
        /// 配置文件持久化路径
        /// </summary>
        private static string settingsPath = "store\\settings.json";

        /// <summary>
        /// 持久化目录
        /// </summary>
        private static string storeDir = "store";

        /// <summary>
        /// css目录
        /// </summary>
        private static string cssDir = "html\\css";

        /// <summary>
        /// 默认显示页面
        /// </summary>
        private static string defaultHtml = "html\\index.html";

        private IntPtr activeHwnd = IntPtr.Zero;
        /// <summary>
        /// 浏览器
        /// </summary>
        ChromiumWebBrowser webView;

        /// <summary>
        /// 复制条目保存记录
        /// </summary>
        List<ClipModel> clipList = new List<ClipModel>(maxRecords + 2);

        /// <summary>
        /// 用于显示的记录列表
        /// </summary>
        List<ClipModel> displayList = new List<ClipModel>(maxRecords);


        /// <summary>
        /// 供浏览器JS回调的接口
        /// </summary>
        CallbackObjectForJs cbOjb;

        /// <summary>
        /// 是否显示开发者工具
        /// </summary>
        public static bool isDevTools = false;

        public static bool isSearch = false;

        /// <summary>
        /// 剪切板事件
        /// </summary>
        private static int WM_CLIPBOARDUPDATE = 0x031D;

        /// <summary>
        /// JSON设置
        /// </summary>
        JsonSerializerSettings displayJsonSettings = new JsonSerializerSettings();

        /// <summary>
        /// 注册快捷键全局原子字符串 
        /// </summary>
        private static string hotkeyAtomStr = "clipPlusAtom...";
        /// <summary>
        /// 快捷键全局原子
        /// </summary>
        private static int hotkeyAtom;

        /// <summary>
        /// 快捷键修饰键
        /// </summary>
        private static int hotkeyModifier = (int)HotKeyManager.KeyModifiers.Alt;
        /// <summary>
        /// 快捷键按键
        /// </summary>
        private static int hotkeyKey = (int)System.Windows.Forms.Keys.V;


        /// <summary>
        /// 是否开机启动
        /// </summary>
        private static bool autoStartup = false;

        /// <summary>
        /// 默认保存记录数
        /// </summary>
        private static int currentRecords = 100;

        /// <summary>
        /// 允许保存的最大记录数
        /// </summary>
        private static int maxRecords = 300;


        /// <summary>
        /// 默认皮肤
        /// </summary>
        private static string skinName = "stand";


        /// <summary>
        /// 默认支持格式
        /// </summary>
        public static ClipType supportFormat = ClipType.qq | ClipType.html | ClipType.image | ClipType.file | ClipType.text;

        /// <summary>
        /// 配置项map
        /// </summary>
        private static Dictionary<String, String> settingsMap = new Dictionary<string, string>();


        /// <summary>
        /// 托盘图标
        /// </summary>
        private System.Windows.Forms.NotifyIcon notifyIcon = null;

        /// <summary>
        /// 当前应用句柄
        /// </summary>
        private IntPtr wpfHwnd;

        /// <summary>
        /// 定时器，用于定时持久化条目
        /// </summary>
        System.Windows.Forms.Timer saveDataTimer = new System.Windows.Forms.Timer();


        /// <summary>
        /// 用于连续粘贴 ，连续粘贴条目列表
        /// </summary>
        List<ClipModel> batchPasteList = new List<ClipModel>();
        /// <summary>
        /// 用于连续粘贴，Shift+鼠标选择 是否按下Shift键
        /// </summary>
        volatile bool isPressedShift = false;
        /// <summary>
        /// 用于连续粘贴，保存上一次选择的index
        /// </summary>
        private int lastSelectedIndex = -1;


        /// <summary>
        /// 当前选中行
        /// </summary>
        public int selectedIndex = -1;


        /// <summary>
        /// 预览窗口
        /// </summary>
        private PreviewForm preview;


        public MainWindow()
        {
            InitializeComponent();

            System.IO.Directory.SetCurrentDirectory(System.Windows.Forms.Application.StartupPath);
            //序列化到前端时只序列化需要的字段
            displayJsonSettings.ContractResolver = new LimitPropsContractResolver(new string[] { "Type", "DisplayValue" });



        }




        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            Console.WriteLine("load");
            if (!Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }

            if (!Directory.Exists(storeDir))
            {
                Directory.CreateDirectory(storeDir);
            }

            //如果配置文件存在则读取配置文件，否则按默认值设置
            if (File.Exists(settingsPath))
            {
                InitConfig();
            }


            if (File.Exists(storePath))
            {
                InitStore();
            }



            InitWebView();

            InitialTray();


            hotkeyAtom = HotKeyManager.GlobalAddAtom(hotkeyAtomStr);


            bool status = HotKeyManager.RegisterHotKey(wpfHwnd, hotkeyAtom, hotkeyModifier, hotkeyKey);

            if (!status)
            {
                Hotkey_Click(null, null);
            }

            InitPreviewForm();

        }




        /// <summary>
        /// 加载持久化数据
        /// </summary>
        private void InitStore()
        {
            string lastSaveImg = string.Empty;

            //从持久化文件中读取复制条目，并将图片类型的条目记录至lastSaveImag，供清除过期图片用
            string json = File.ReadAllText(storePath);

            List<ClipModel> list = JsonConvert.DeserializeObject<List<ClipModel>>(json);
            foreach (ClipModel clip in list)
            {
                clipList.Add(clip);
                if (clip.Type == IMAGE_TYPE)
                {
                    lastSaveImg += clip.ClipValue;
                }
            }
            saveDataTimer.Tick += BatchPasteTimer_Tick;
            saveDataTimer.Interval = 60000;
            saveDataTimer.Start();
            new Thread(new ParameterizedThreadStart(ClearExpireImage)).Start(lastSaveImg);

        }

        /// <summary>
        /// 初始化预览窗口
        /// </summary>
        private void InitPreviewForm()
        {
            preview = new PreviewForm(this);
            preview.Focusable = false;
            preview.IsHitTestVisible = false;
            preview.IsTabStop = false;
            preview.ShowInTaskbar = false;
            preview.ShowActivated = false;
        }
        private static void InitConfig()
        {
            //从持久化文件中读取设置项
            string json = File.ReadAllText(settingsPath);
            settingsMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            if (settingsMap.ContainsKey("startup"))
            {
                autoStartup = bool.Parse(settingsMap["startup"]);
                if (autoStartup)
                {
                    SetStartup(autoStartup);
                }
            }
            if (settingsMap.ContainsKey("skin"))
            {
                skinName = settingsMap["skin"];
            }
            if (settingsMap.ContainsKey("record"))
            {
                currentRecords = int.Parse(settingsMap["record"]);
            }
            if (settingsMap.ContainsKey("key"))
            {
                hotkeyKey = int.Parse(settingsMap["key"]);
                hotkeyModifier = int.Parse(settingsMap["modifier"]);

            }
            if (settingsMap.ContainsKey("format"))
            {
                supportFormat = (ClipType)int.Parse(settingsMap["format"]);
            }
        }

        /// <summary>
        /// 初始化浏览器
        /// </summary>
        private void InitWebView()
        {
            ///初始化浏览器
            var setting = new CefSharp.CefSettings();
            setting.Locale = "zh-CN";
            setting.LogSeverity = LogSeverity.Disable;
            setting.WindowlessRenderingEnabled = true;
            setting.CefCommandLineArgs.Add("Cache-control", "no-cache");
            setting.CefCommandLineArgs.Add("Pragma", "no-cache");
            setting.CefCommandLineArgs.Add("expries", "-1");
            setting.CefCommandLineArgs.Add("disable-gpu", "1");
            CefSharp.Cef.Initialize(setting);
            webView = new ChromiumWebBrowser();
            webView.MenuHandler = new MenuHandler();
            BrowserSettings browserSetting = new BrowserSettings();
            browserSetting.ApplicationCache = CefState.Disabled;
            browserSetting.DefaultEncoding = "utf-8";
            webView.BrowserSettings = browserSetting;
            webView.Address = "file:///" + defaultHtml;

            webView.KeyDown += Window_KeyDown;
            webView.KeyUp += Window_KeyUp;
            cbOjb = new CallbackObjectForJs(this);
            webView.RegisterAsyncJsObject("callbackObj", cbOjb);

            mainGrid.Children.Add(webView);
            webView.SetValue(Grid.RowProperty, 1);



        }

        private void BatchPasteTimer_Tick(object sender, EventArgs e)
        {
            SaveData(clipList, storePath);
        }

        /// <summary>
        /// 删除指定索引的数据
        /// </summary>
        /// <param name="index"></param>
        public void DeleteClip(int index)
        {


            //同时删除显示列表和保存列表中的条目
            ClipModel clip = displayList[index];
            displayList.RemoveAt(index);
            Console.WriteLine("index:"+index);
            Console.WriteLine("id:" + clip.SourceId);
            clipList.RemoveAt(clip.SourceId);

            ShowList();

            SaveData(clipList, storePath);
           


        }

        /// <summary>
        /// 初始化托盘图标及菜单
        /// </summary>
        private void InitialTray()
        {
            string productName = System.Windows.Forms.Application.ProductName;
            //设置托盘图标
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Text = productName;

            StreamResourceInfo info = Application.GetResourceStream(new Uri("/" + productName + ".ico", UriKind.Relative));
            Stream s = info.Stream;
            notifyIcon.Icon = new System.Drawing.Icon(s);
            notifyIcon.Visible = true;



            //设置菜单项
            System.Windows.Forms.MenuItem exit = new System.Windows.Forms.MenuItem("退出");
            System.Windows.Forms.MenuItem separator0 = new System.Windows.Forms.MenuItem("-");
            System.Windows.Forms.MenuItem startup = new System.Windows.Forms.MenuItem("开机自启");
            System.Windows.Forms.MenuItem devTools = new System.Windows.Forms.MenuItem("调试工具");

            System.Windows.Forms.MenuItem separator1 = new System.Windows.Forms.MenuItem("-");
            System.Windows.Forms.MenuItem hotkey = new System.Windows.Forms.MenuItem("热键");

            System.Windows.Forms.MenuItem record = new System.Windows.Forms.MenuItem("记录数");
            System.Windows.Forms.MenuItem separator2 = new System.Windows.Forms.MenuItem("-");
            System.Windows.Forms.MenuItem skin = new System.Windows.Forms.MenuItem("皮肤");
            System.Windows.Forms.MenuItem reload = new System.Windows.Forms.MenuItem("刷新");
            System.Windows.Forms.MenuItem second = new System.Windows.Forms.MenuItem("高亮第二条");
            System.Windows.Forms.MenuItem separator3 = new System.Windows.Forms.MenuItem("-");
            System.Windows.Forms.MenuItem format = new System.Windows.Forms.MenuItem("格式");
            System.Windows.Forms.MenuItem clear = new System.Windows.Forms.MenuItem("清空");


            System.Windows.Forms.MenuItem qqFormat = new System.Windows.Forms.MenuItem("腾讯");
            System.Windows.Forms.MenuItem htmlFormat = new System.Windows.Forms.MenuItem("html");
            System.Windows.Forms.MenuItem imageFormat = new System.Windows.Forms.MenuItem("图片");
            System.Windows.Forms.MenuItem fileFormat = new System.Windows.Forms.MenuItem("格式");
            System.Windows.Forms.MenuItem txtFormat = new System.Windows.Forms.MenuItem("格式");


            devTools.Click += DevTools_Click;
            clear.Click += Clear_Click;
            reload.Click += new EventHandler(Reload);
            exit.Click += new EventHandler(Exit_Click);
            hotkey.Click += Hotkey_Click;
            startup.Click += Startup_Click;
            startup.Checked = autoStartup;


            //增加记录数设置子菜单项
            for (int i = 100; i <= maxRecords; i += 100)
            {

                string recordsNum = i.ToString();
                System.Windows.Forms.MenuItem subRecord = new System.Windows.Forms.MenuItem(recordsNum);
                if (int.Parse(recordsNum) == currentRecords)
                {
                    subRecord.Checked = true;
                }
                subRecord.Click += RecordSet_Click;
                record.MenuItems.Add(subRecord);

            }

            //增加格式选择子菜单项
            foreach (ClipType type in Enum.GetValues(typeof(ClipType)))
            {

                System.Windows.Forms.MenuItem subFormat = new System.Windows.Forms.MenuItem(Enum.GetName(typeof(ClipType), type));
                subFormat.Tag = type;
                if ((supportFormat & type) != 0)
                {
                    subFormat.Checked = true;

                }
                if (type == ClipType.text)
                {
                    subFormat.Enabled = false;
                }
                else
                {
                    subFormat.Click += SubFormat_Click;
                }
                format.MenuItems.Add(subFormat);
            }

            //根据css文件创建皮肤菜单项
            if (Directory.Exists(cssDir))
            {
                List<string> fileList = Directory.EnumerateFiles(cssDir).ToList();

                foreach (string file in fileList)
                {

                    string fileName = Path.GetFileNameWithoutExtension(file);
                    System.Windows.Forms.MenuItem subRecord = new System.Windows.Forms.MenuItem(fileName);
                    if (skinName.Equals(fileName.ToLower()))
                    {
                        subRecord.Checked = true;


                    }
                    subRecord.Tag = file;
                    skin.MenuItems.Add(subRecord);
                    subRecord.Click += SkinItem_Click;
                }
            }



            //关联菜单项至托盘
            System.Windows.Forms.MenuItem[] childen = new System.Windows.Forms.MenuItem[] { clear, format, separator3, reload, skin, separator2, record, hotkey, separator1, devTools, startup, separator0, exit };
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(childen);


        }

        private void SubFormat_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.MenuItem item = (System.Windows.Forms.MenuItem)sender;
            if (item.Checked)
            {
                item.Checked = false;
                supportFormat = supportFormat & ~((ClipType)item.Tag);


            }
            else
            {

                item.Checked = true;
                supportFormat = supportFormat | ((ClipType)item.Tag);
            }
            settingsMap["format"] = ((int)supportFormat).ToString();
            SaveSettings();
        }

        private void DevTools_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.MenuItem item = (System.Windows.Forms.MenuItem)sender;
            if (!isDevTools)
            {
                item.Checked = true;
                isDevTools = true;

                webView?.GetBrowser()?.ShowDevTools();
                ShowWindowAndList();
            }
            else
            {
                item.Checked = false;
                webView?.GetBrowser()?.CloseDevTools();
                isDevTools = false;
                DiyHide();
            }
        }

        private void SkinItem_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.MenuItem item = (System.Windows.Forms.MenuItem)sender;
            foreach (System.Windows.Forms.MenuItem i in item.Parent.MenuItems)
            {
                i.Checked = false;
            }
            item.Checked = true;
            settingsMap["skin"] = item.Text;
            SaveSettings();
            string css = item.Tag.ToString();
            ChangeSkin(css);

        }


        /// <summary>
        /// 通过修改index.html中引入的样式文件来换肤
        /// </summary>
        /// <param name="cssPath"></param>
        private void ChangeSkin(string cssPath)
        {

            cssPath = cssPath.Replace("\\", "/").Replace("html/", "");
            string[] fileLines = File.ReadAllLines(defaultHtml);
            fileLines[fileLines.Length - 1] = " <link rel='stylesheet' type='text/css' href='" + cssPath + "'/>";
            File.WriteAllLines(defaultHtml, fileLines, Encoding.UTF8);
            webView.GetBrowser().Reload();


        }

        /// <summary>
        /// 设置是否开机启动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Startup_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.MenuItem item = (System.Windows.Forms.MenuItem)sender;
            item.Checked = !item.Checked;

            SetStartup(item.Checked);
            settingsMap["startup"] = item.Checked.ToString();
            SaveSettings();
        }

        /// <summary>
        /// 设置热键
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Hotkey_Click(object sender, EventArgs e)
        {
            SetHotKeyForm sethk = new SetHotKeyForm();
            sethk.HotkeyKey = hotkeyKey;
            sethk.HotkeyModifier = hotkeyModifier;
            sethk.WpfHwnd = wpfHwnd;
            sethk.HotkeyAtom = hotkeyAtom;
            if (sethk.ShowDialog() == true)
            {

                hotkeyKey = sethk.HotkeyKey;
                hotkeyModifier = sethk.HotkeyModifier;

                settingsMap["modifier"] = hotkeyModifier.ToString();
                settingsMap["key"] = hotkeyKey.ToString();
                SaveSettings();
            }
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        private void SaveSettings()
        {
            string json = JsonConvert.SerializeObject(settingsMap);
            File.WriteAllText(settingsPath, json);
        }


        /// <summary>
        /// 设置保存记录数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecordSet_Click(object sender, EventArgs e)
        {

            System.Windows.Forms.MenuItem item = ((System.Windows.Forms.MenuItem)sender);
            foreach (System.Windows.Forms.MenuItem i in item.Parent.MenuItems)
            {
                i.Checked = false;
            }
            item.Checked = true;
            currentRecords = int.Parse(item.Text);
            settingsMap["record"] = item.Text;
            SaveSettings();



        }

        /// <summary>
        /// 清空所有条目
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Clear_Click(object sender, EventArgs e)
        {
            clipList.Clear();
            SaveData(clipList, storePath);
        }

        /// <summary>
        /// 刷新页面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Reload(object sender, EventArgs e)
        {
            webView.GetBrowser().Reload(true);
        }

        private void Exit_Click(object sender, EventArgs e)
        {

            Application.Current.Shutdown();

        }



        public IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {

            //当为剪切板消息时，由于获取数据会有失败的情况，所以循环3次，尽量确保成功
            if (msg == WM_CLIPBOARDUPDATE)
            {

                if (txtSearch.IsFocused)
                {
                    return IntPtr.Zero;
                }
                ClipModel clip = new ClipModel();


                //处理剪切板QQ自定义格式
                if ((supportFormat & ClipType.qq) != 0 && Clipboard.ContainsData(QQ_RICH_TYPE))
                {
                    HandleClipQQ(clip);

                }
                //处理图片
                else if ((supportFormat & ClipType.image) != 0 && (Clipboard.ContainsImage() || Clipboard.ContainsData(DataFormats.Dib)))
                {
                    HandleClipImage(clip);

                }
                //处理剪切板文件
                else if (Clipboard.ContainsText())
                {
                    HandClipText(clip);

                }
                //处理HTML类型
                else if ((supportFormat & ClipType.html) != 0 && Clipboard.ContainsData(DataFormats.Html))
                {
                    HandleClipHtml(clip);

                }





                //处理剪切板文件
                else if ((supportFormat & ClipType.file) != 0 && Clipboard.ContainsFileDropList())
                {
                    HandleClipFile(clip);

                }

                else
                {

                    return IntPtr.Zero;
                }
                if (string.IsNullOrWhiteSpace(clip.ClipValue))
                {
                    return IntPtr.Zero;
                }
                if (clipList.Count > 0 && clip.ClipValue == clipList[0].ClipValue)
                {
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

            clipList.Insert(0, clip);

            await ClearImage();



        }

        /// <summary>
        /// 清理多余条目，如果为图片类型则清理关联的图片
        /// </summary>
        /// <returns></returns>
        private Task ClearImage()
        {
            return Task.Run(() =>
            {
                if (clipList.Count > currentRecords)
                {
                    DeleteClip(currentRecords);
                }


            });

        }

        /// <summary>
        /// 显示窗口并列出所有条目
        /// </summary>
        private void ShowWindowAndList()
        {
            displayList.Clear();
            int displayHeight = 0;
            for (int i = 0; i < clipList.Count; i++)
            {
                clipList[i].SourceId = i;
                displayList.Add(clipList[i]);
                displayHeight += clipList[i].Height;
            }

            ShowList();

            activeHwnd = WinAPIHelper.GetForegroundWindow();
            WinAPIHelper.POINT point = new WinAPIHelper.POINT();
             
            if (WinAPIHelper.GetCursorPos(out point))
            {
                if (clipList.Count == 0)
                {
                    this.Height = 100;
                }
                else
                {
                    this.Height = displayHeight + 25;


                }


                double x = SystemParameters.WorkArea.Width;//得到屏幕工作区域宽度
                double y = SystemParameters.WorkArea.Height;//得到屏幕工作区域高度
                double mx = CursorHelp.ConvertPixelsToDIPixels(point.X);
                double my = CursorHelp.ConvertPixelsToDIPixels(point.Y);

                if (mx > x - this.ActualWidth)
                {
                    this.Left = x - this.ActualWidth;
                }
                else
                {
                    this.Left = mx - 2;
                }
                if (my > y - this.ActualHeight)
                {
                    this.Top = y - this.ActualHeight;
                }
                else
                {
                    this.Top = my - 2;
                }


                DiyShow();



                webView.Focus();

            }


        }

        private void ShowList()
        {
            displayList.Clear();
            int displayHeight = 0;
            for (int i = 0; i < clipList.Count; i++)
            {
                clipList[i].SourceId = i;
                displayList.Add(clipList[i]);
                displayHeight += clipList[i].Height;
            }
            string json = JsonConvert.SerializeObject(displayList, displayJsonSettings);

            json = HttpUtility.UrlEncode(json);

            selectedIndex = 1;

            webView.GetBrowser().MainFrame.ExecuteJavaScriptAsync("showList('" + json + "',1)");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveData(clipList, storePath);

            if (notifyIcon != null)
            {
                notifyIcon.Dispose();
            }
            try
            {
                if (webView != null)
                {
                    webView.GetBrowser().CloseBrowser(true);
                    webView.Dispose();
                    Cef.Shutdown();
                }
            }
            catch { }

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
        public void PreviewByIndex(int id)
        {


            this.Dispatcher.Invoke(
           new Action(
         delegate
         {
             ShowPreviewForm(id);

         }));



        }

        /// <summary>
        /// 根据索引粘贴条目到活动窗口
        /// </summary>
        /// <param name="id">索引</param>
        public void PasteValueByIndex(int id)
        {
            if (id >= displayList.Count)
            {
                return;
            }
            //当按下shift键时，做批量处理判断
            if (isPressedShift)
            {
                if (lastSelectedIndex == -1)
                {

                    lastSelectedIndex = id;

                }
                else
                {

                    SetBatchPatse(id, lastSelectedIndex);

                    this.Dispatcher.Invoke(
                          new Action(
                        delegate
                        {

                            DiyHide();
                            preview.Hide();
                        }));
                    new Thread(new ParameterizedThreadStart(BatchPaste)).Start(false);
                    lastSelectedIndex = -1;
                    isPressedShift = false;
                }
            }
            else  //单条处理
            {

                this.Dispatcher.Invoke(
               new Action(
             delegate
             {
                 DiyHide();

                 preview.Hide();


             }));

                //从显示列表中获取记录，并根据sourceId从对保存列表中的该条记录做相应处理
                ClipModel result = displayList[id];

                clipList.RemoveAt(result.SourceId);

                if (result.Type == FILE_TYPE)
                {
                    string[] files = result.ClipValue.Split(',');
                    foreach (string str in files)
                    {
                        if (!File.Exists(str))
                        {
                            this.Dispatcher.Invoke(
                         new Action(
                       delegate
                       {
                           MessageBox.Show("源文件缺失，粘贴失败！");
                       }));
                            return;
                        }
                    }
                }

                clipList.Insert(0, result);

                //加入待粘贴列表
                batchPasteList.Clear();
                batchPasteList.Add(result);

                new Thread(new ParameterizedThreadStart(BatchPaste)).Start(true);


            }


        }

        /// <summary>
        /// 隐藏预览窗口
        /// </summary>
        public void HidePreview()
        {

            this.Dispatcher.Invoke(
              new Action(
            delegate
            {
                if (preview.IsVisible)
                    preview.Hide();
            }));



        }

        /// <summary>
        /// 显示图片预览窗口
        /// </summary>
        /// <param name="id"></param>
        private void ShowPreviewForm(int id)
        {
            preview.Hide();
            ClipModel result = clipList[id];
            if (result.Type == IMAGE_TYPE)
            {
                preview.ImgPath = result.ClipValue;

                preview.Show();

            }

        }
        /// <summary>
        /// 粘贴条目到活动窗口 
        /// </summary>
        /// <param name="result">需要粘贴的值</param>
        /// /// <param name="neadPause">是否需要延时，单条需要，批量不需要</param>
        private void SetValueToClip(ClipModel result, bool neadPause)
        {


            //设置剪切板前取消监听
            WinAPIHelper.RemoveClipboardFormatListener(wpfHwnd);

            ClipService.SetValueToClip(result);
            //设置剪切板后恢复监听
            WinAPIHelper.AddClipboardFormatListener(wpfHwnd);

            System.Windows.Forms.SendKeys.SendWait("^v");




        }



        private void Window_Deactivated(object sender, EventArgs e)
        {

            WindowLostFocusHandle();
        }

        /// <summary>
        /// 当窗口失去焦点时的处理
        /// </summary>
        private void WindowLostFocusHandle()
        {

            lastSelectedIndex = -1;
            isPressedShift = false;
            if (preview != null)
            {
                preview.Hide();
            }
            if (!isDevTools)
            {
                DiyHide();
            }


        }


        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                if (!searchStack.IsVisible)
                {
                    searchStack.Visibility = Visibility.Visible;
                }
                if (!txtSearch.IsFocused)
                {
                    txtSearch.Focus();
                }
                this.Height += 35;
                return;
            }


            if (displayList.Count > 0)
            {

                if (e.KeyboardDevice.Modifiers == ModifierKeys.Shift)  //处理基于SHIFT+数字的批量粘贴
                {

                    int keyNum = (int)e.Key - 34;

                    if (keyNum >= 0 && keyNum <= 35)
                    {
                        if (lastSelectedIndex == -1)
                        {
                            lastSelectedIndex = keyNum - 1;
                        }
                        else
                        {
                            int currentKey = keyNum - 1;

                            SetBatchPatse(currentKey, lastSelectedIndex);
                            DiyHide();
                            new Thread(new ParameterizedThreadStart(BatchPaste)).Start(false);
                            lastSelectedIndex = -1;

                        }
                        return;
                    }

                    isPressedShift = true;
                }


                else if (!isDevTools)//处理单选粘贴
                {

                    int index = -1;

                    if (e.Key == Key.Space)
                    {
                        //按空格直接返回第0行
                        index = 0;
                    }
                    else if (e.Key == Key.Enter)
                    {
                        index = selectedIndex;
                    }

                    else
                    {
                        int keyNum = (int)e.Key - 34;
                        if (keyNum > 0 && keyNum <= 35)
                        {
                            index = keyNum - 1;

                        }
                    }
                    if (index >= 0)
                    {

                        PasteValueByIndex(index);
                    }

                }
            }

        }



        /// <summary>
        /// 根据给点起始、结束索引来设置批量粘贴条目
        /// </summary>
        /// <param name="nowIndex">结束索引</param>
        /// <param name="lastIndex">起始索引</param>
        private void SetBatchPatse(int nowIndex, int lastIndex)
        {
            batchPasteList.Clear();
            if (nowIndex > lastIndex)
            {
                for (int i = lastIndex; i <= nowIndex; i++)
                {
                    var result = displayList[i];

                    clipList.RemoveAt(result.SourceId);
                    if (result.Type == FILE_TYPE)
                    {
                        string[] files = result.ClipValue.Split(',');
                        foreach (string str in files)
                        {
                            if (!File.Exists(str))
                            {
                                this.Dispatcher.Invoke(
                             new Action(
                           delegate
                           {
                               MessageBox.Show("粘贴列表中部分源文件缺失，粘贴失败！");
                           }));
                                return;
                            }
                        }
                    }
                    clipList.Insert(0, result);
                    batchPasteList.Add(result);


                }

            }
            else
            {
                for (int i = lastIndex; i >= nowIndex; i--)
                {
                    var result = clipList[lastIndex];

                    clipList.RemoveAt(result.SourceId);
                    if (result.Type == FILE_TYPE)
                    {
                        string[] files = result.ClipValue.Split(',');
                        foreach (string str in files)
                        {
                            if (!File.Exists(str))
                            {
                                this.Dispatcher.Invoke(
                             new Action(
                           delegate
                           {
                               MessageBox.Show("粘贴列表中部分源文件缺失，粘贴失败！");
                           }));
                                return;
                            }
                        }
                    }
                    clipList.Insert(0, result);
                    batchPasteList.Add(result);
                }

            }
        }

        /// <summary>
        /// 批量粘贴，由于循环太快、发送粘贴按键消息太慢，故延时200ms
        /// </summary>
        /// <param name="needPause"></param>
        private void BatchPaste(object needPause)
        {

            for (int i = 0; i < batchPasteList.Count; i++)
            {

                this.Dispatcher.Invoke(
                      new Action(
                    delegate
                    {
                        SetValueToClip(batchPasteList[i], (bool)needPause);
                    }));
                Thread.Sleep(200);
            }


        }



        private void Window_KeyUp(object sender, KeyEventArgs e)
        {

            if (e.Key == Key.Back && row0.Height.Value != 0)
            {
                txtSearch.Focus();
                return;
            }
            if (e.Key == Key.F12)
            {

                webView.GetBrowser().MainFrame.ViewSource();

                return;
            }

            if (e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
            {
                //当shift键keyUp时，还原状态
                isPressedShift = false;
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {

            if (searchStack.Visibility != Visibility.Visible)
            {
                return;
            }
            displayList.Clear();
            string value = txtSearch.Text.ToLower();

            for (int i = 0; i < clipList.Count; i++)
            {
                if (clipList[i].Type == value.Trim() || clipList[i].ClipValue.ToLower().IndexOf(value) >= 0)
                {
                    clipList[i].SourceId = i;
                    displayList.Add(clipList[i]);
                }
            }
            string json = JsonConvert.SerializeObject(displayList, displayJsonSettings);

            json = HttpUtility.UrlEncode(json);

            selectedIndex = (value == "") ? 1 : 0;

            webView.GetBrowser().MainFrame.ExecuteJavaScriptAsync("showList('" + json + "'," + selectedIndex + ")");




        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {

            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                //先clear再隐藏，保证隐藏前再执行一次无条件查询
                if (txtSearch.Text != "")
                {
                    txtSearch.Clear();
                }
                if (searchStack.IsVisible)
                {
                    searchStack.Visibility = Visibility.Collapsed;
                }

                this.Height -= 35;


            }

            else if (e.Key == Key.Enter)
            {

                PasteValueByIndex(selectedIndex);
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

            DiyHide();

        }

        private void DiyShow()
        {
            this.Topmost = true;

            this.Activate();
            this.Opacity = 100;

        }

        /// <summary>
        /// 通过把透明度设置为0来代替窗体隐藏，防止到窗体显示时才开始渲染
        /// </summary>
        private void DiyHide()
        {

            this.Topmost = false;
            WinAPIHelper.SetForegroundWindow(activeHwnd);
            if (searchStack.IsVisible)
            {
                //先隐藏后clear,防止多余的查询操作
                searchStack.Visibility = Visibility.Collapsed;
                txtSearch.Clear();

                this.Height -= 35;
            }
            webView?.GetBrowser()?.MainFrame.ExecuteJavaScriptAsync("scrollTop()");


            this.Opacity = 0;



        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Source is TextBox)
            {
                if (e.Key == Key.Down && displayList.Count > 0)
                {
                    if (txtSearch.Text == "")
                    {
                        selectedIndex = 0;
                        webView.GetBrowser().MainFrame.ExecuteJavaScriptAsync("selectItem(0)");
                    }

                    webView.Focus();
                    e.Handled = true;

                }

            }
            else
            {
                if (e.Key == Key.Down)
                {
                    if (selectedIndex < displayList.Count - 1)
                    {
                        selectedIndex++;
                        webView.GetBrowser().MainFrame.ExecuteJavaScriptAsync("selectItem(" + selectedIndex + ")");
                    }
                    e.Handled = true;

                }
                else if (e.Key == Key.Up)
                {
                    if (selectedIndex == 0)
                    {
                        if (searchStack.Visibility == Visibility.Visible)
                        {
                            txtSearch.Focus();
                        }
                    }
                    if (selectedIndex > 0)
                    {
                        selectedIndex--;
                        webView.GetBrowser().MainFrame.ExecuteJavaScriptAsync("selectItem(" + selectedIndex + ")");
                    }

                    e.Handled = true;
                }
                else
                {

                    e.Handled = false;
                }
            }
        }


        internal class MenuHandler : IContextMenuHandler
        {
            public void OnBeforeContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
            {
                model.Clear();
            }

            public bool OnContextMenuCommand(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
            {
                return false;
            }

            public void OnContextMenuDismissed(IWebBrowser browserControl, IBrowser browser, IFrame frame)
            {

            }

            public bool RunContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
            {
                return false;
            }
        }


    }


}

