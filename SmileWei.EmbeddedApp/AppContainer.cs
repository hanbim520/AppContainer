﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Drawing.Design;
using System.Windows.Forms;
using System.IO;
using System.Drawing;

namespace SmileWei.EmbeddedApp
{
    /// <summary>
    /// 可以把其他窗体应用程序嵌入此容器
    /// </summary>
    [ToolboxBitmap(typeof(AppContainer), "AppControl.bmp")]
    public partial class AppContainer : System.Windows.Forms.Panel
    {
        Action<object, EventArgs> appIdleAction = null;
        EventHandler appIdleEvent = null;
        public AppContainer(bool showEmbedResult = false)
        {
            InitializeComponent();
            this.ShowEmbedResult = showEmbedResult;
            appIdleAction = new Action<object, EventArgs>(Application_Idle);
            appIdleEvent = new EventHandler(appIdleAction);
        }

        public AppContainer(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
            appIdleAction = new Action<object, EventArgs>(Application_Idle);
            appIdleEvent = new EventHandler(appIdleAction);
        }
        /// <summary>
        /// 将属性<code>AppFilename</code>指向的应用程序打开并嵌入此容器
        /// </summary>
        public void Start()
        {
            if (m_AppProcess != null)
            {
                Stop();
            }
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(this.m_AppFilename);
                info.UseShellExecute = true;
                info.WindowStyle = ProcessWindowStyle.Minimized;
                //info.WindowStyle = ProcessWindowStyle.Hidden;
                m_AppProcess = System.Diagnostics.Process.Start(info);
                // Wait for process to be created and enter idle condition
                m_AppProcess.WaitForInputIdle();
                //todo:下面这两句会引发 NullReferenceException 异常，不知道怎么回事                
                //m_AppProcess.Exited += new EventHandler(m_AppProcess_Exited);
                //m_AppProcess.EnableRaisingEvents = true;
                Application.Idle += appIdleEvent;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format("{1}{0}{2}{0}{3}"
                    ,Environment.NewLine
                    , "*" + ex.ToString()
                    , "*StackTrace:" + ex.StackTrace
                    ,"*Source:"+ex.Source
                    ), "内嵌程序加载失败");
                if (m_AppProcess != null)
                {
                    if (!m_AppProcess.HasExited)
                        m_AppProcess.Kill();
                    m_AppProcess = null;
                }
            }
            
        }
        /// <summary>
        /// 确保应用程序嵌入此容器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Application_Idle(object sender, EventArgs e)
        {
            if (this.m_AppProcess == null || this.m_AppProcess.HasExited)
            {
                this.m_AppProcess = null;
                Application.Idle -= appIdleEvent;
                return;
            }
            if (m_AppProcess.MainWindowHandle == IntPtr.Zero) return;
            //Application.Idle -= appIdleEvent;
            if (EmbedProcess(m_AppProcess, this))
            { Application.Idle -= appIdleEvent; }
            //ShowWindow(m_AppProcess.MainWindowHandle, SW_SHOWNORMAL);
            //var parent = GetParent(m_AppProcess.MainWindowHandle);//你妹，不管用，全是0
            //if (parent == this.Handle)
            //{
            //    Application.Idle -= appIdleEvent;
            //}
        }
        /// <summary>
        /// 应用程序结束运行时要清除这里的标识
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void m_AppProcess_Exited(object sender, EventArgs e)
        {
            m_AppProcess = null;
        }
        //public void Start(string appFilename)
        //{
        //    this.AppFilename = AppFilename;
        //    Start();
        //}
        /// <summary>
        /// 将属性<code>AppFilename</code>指向的应用程序关闭
        /// </summary>
        public void Stop()
        {
            if (m_AppProcess != null)// && m_AppProcess.MainWindowHandle != IntPtr.Zero)
            {
                try
                {
                    if (!m_AppProcess.HasExited)
                        m_AppProcess.Kill();
                }
                catch (Exception)
                {
                }
                m_AppProcess = null;
                //state = EmbedState.none;
                embedResult = 0;
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            Stop();
            base.OnHandleDestroyed(e);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            if (m_AppProcess != null)
            {
                MoveWindow(m_AppProcess.MainWindowHandle, 0, 0, this.Width, this.Height, true);
            }
            base.OnResize(eventargs);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            this.Invalidate();
            base.OnSizeChanged(e);
        }

        #region 属性
        /// <summary>
        /// application process
        /// </summary>
        Process m_AppProcess = null;
        public Process AppProcess {
            get { return this.m_AppProcess; }
            set { this.m_AppProcess = value; }
        }

        /// <summary>
        /// 要嵌入的程序文件名
        /// </summary>
        private string m_AppFilename = "";
        /// <summary>
        /// 要嵌入的程序文件名
        /// </summary>
        [Category("Data")]
        [Description("要嵌入的程序文件名")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        [Editor(typeof(AppFilenameEditor), typeof(UITypeEditor))]
        public string AppFilename
        {
            get
            {
                return m_AppFilename;
            }
            set
            {
                if (value == null || value == m_AppFilename) return;
                var self = Application.ExecutablePath;
                if (value.ToLower() == self.ToLower())
                {
                    MessageBox.Show("不能自己嵌入自己！", "SmileWei.EmbeddedApp");
                    return;
                }
                if (!value.ToLower().EndsWith(".exe"))
                {
                    MessageBox.Show("要嵌入的文件扩展名不是exe！", "SmileWei.EmbeddedApp");
                }
                if (!File.Exists(value))
                {
                    MessageBox.Show("要嵌入的程序不存在！", "SmileWei.EmbeddedApp");
                    return;
                }
                m_AppFilename = value;
            }
        }
        /// <summary>
        /// 标识内嵌程序是否已经启动
        /// </summary>
        public bool IsStarted { get { return (this.m_AppProcess != null); } }
        
        #endregion 属性

        #region Win32 API
        [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId", SetLastError = true,
             CharSet = CharSet.Unicode, ExactSpelling = true,
             CallingConvention = CallingConvention.StdCall)]
        private static extern long GetWindowThreadProcessId(long hWnd, long lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongA", SetLastError = true)]
        private static extern long GetWindowLong(IntPtr hwnd, int nIndex);

        public static IntPtr SetWindowLong(HandleRef hWnd, int nIndex, int dwNewLong)
        {
            if (IntPtr.Size == 4)
            {
                return SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
            }
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        }
        [DllImport("user32.dll", EntryPoint = "SetWindowLong", CharSet = CharSet.Auto)]
        public static extern IntPtr SetWindowLongPtr32(HandleRef hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", CharSet = CharSet.Auto)]
        public static extern IntPtr SetWindowLongPtr64(HandleRef hWnd, int nIndex, int dwNewLong);
 
        [DllImport("user32.dll", SetLastError = true)]
        private static extern long SetWindowPos(IntPtr hwnd, long hWndInsertAfter, long x, long y, long cx, long cy, long wFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int cx, int cy, bool repaint);

        [DllImport("user32.dll", EntryPoint = "PostMessageA", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hwnd, uint Msg, uint wParam, uint lParam);

        /// <summary>
        /// 获取系统错误信息描述
        /// </summary>
        /// <param name="errCode">系统错误码</param>
        /// <returns></returns>
        public static string GetLastError()
        {
            var errCode = Marshal.GetLastWin32Error();
            IntPtr tempptr = IntPtr.Zero;
            string msg = null;
            FormatMessage(0x1300, ref tempptr, errCode, 0, ref msg, 255, ref tempptr);
            return msg;
        }
        /// <summary>
        /// 获取系统错误信息描述
        /// </summary>
        /// <param name="errCode">系统错误码</param>
        /// <returns></returns>
        public static string GetLastErrorString(int errCode)
        {
            IntPtr tempptr = IntPtr.Zero;
            string msg = null;
            FormatMessage(0x1300, ref tempptr, errCode, 0, ref msg, 255, ref tempptr);
            return msg;
        }

        [System.Runtime.InteropServices.DllImport("Kernel32.dll")]
        public extern static int FormatMessage(int flag, ref IntPtr source, int msgid, int langid, ref string buf, int size, ref IntPtr args);


        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetParent(IntPtr hwnd);
        ///// <summary>
        ///// ShellExecute(IntPtr.Zero, "Open", "C:/Program Files/TTPlayer/TTPlayer.exe", "", "", 1);
        ///// </summary>
        ///// <param name="hwnd"></param>
        ///// <param name="lpOperation"></param>
        ///// <param name="lpFile"></param>
        ///// <param name="lpParameters"></param>
        ///// <param name="lpDirectory"></param>
        ///// <param name="nShowCmd"></param>
        ///// <returns></returns>
        //[DllImport("shell32.dll", EntryPoint = "ShellExecute")]
        //public static extern int ShellExecute(
        //IntPtr hwnd,
        //string lpOperation,
        //string lpFile,
        //string lpParameters,
        //string lpDirectory,
        //int nShowCmd
        //);
        //[DllImport("kernel32.dll")]
        //public static extern int OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId); 
        [DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);



        private const int SWP_NOOWNERZORDER = 0x200;
        private const int SWP_NOREDRAW = 0x8;
        private const int SWP_NOZORDER = 0x4;
        private const int SWP_SHOWWINDOW = 0x0040;
        private const int WS_EX_MDICHILD = 0x40;
        private const int SWP_FRAMECHANGED = 0x20;
        private const int SWP_NOACTIVATE = 0x10;
        private const int SWP_ASYNCWINDOWPOS = 0x4000;
        private const int SWP_NOMOVE = 0x2;
        private const int SWP_NOSIZE = 0x1;
        private const int GWL_STYLE = (-16);
        private const int WS_VISIBLE = 0x10000000;
        private const int WM_CLOSE = 0x10;
        private const int WS_CHILD = 0x40000000;

        private const int  SW_HIDE            = 0; //{隐藏, 并且任务栏也没有最小化图标}
        private const int  SW_SHOWNORMAL      = 1; //{用最近的大小和位置显示, 激活}
        private const int  SW_NORMAL          = 1; //{同 SW_SHOWNORMAL}
        private const int  SW_SHOWMINIMIZED   = 2; //{最小化, 激活}
        private const int  SW_SHOWMAXIMIZED   = 3; //{最大化, 激活}
        private const int  SW_MAXIMIZE        = 3; //{同 SW_SHOWMAXIMIZED}
        private const int  SW_SHOWNOACTIVATE  = 4; //{用最近的大小和位置显示, 不激活}
        private const int  SW_SHOW            = 5; //{同 SW_SHOWNORMAL}
        private const int  SW_MINIMIZE        = 6; //{最小化, 不激活}
        private const int  SW_SHOWMINNOACTIVE = 7; //{同 SW_MINIMIZE}
        private const int  SW_SHOWNA          = 8; //{同 SW_SHOWNOACTIVATE}
        private const int  SW_RESTORE         = 9; //{同 SW_SHOWNORMAL}
        private const int  SW_SHOWDEFAULT     = 10; //{同 SW_SHOWNORMAL}
        private const int  SW_MAX             = 10; //{同 SW_SHOWNORMAL}

        //const int PROCESS_ALL_ACCESS = 0x1F0FFF;
        //const int PROCESS_VM_READ = 0x0010;
        //const int PROCESS_VM_WRITE = 0x0020;     
        #endregion Win32 API


        public void EmbedAgain()
        {
            EmbedProcess(m_AppProcess, this);
        }

        //public EmbedState state = EmbedState.none;
        /// <summary>
        /// 如果函数成功，返回值为子窗口的原父窗口句柄；如果函数失败，返回值为NULL。若想获得多错误信息，请调用GetLastError函数。
        /// </summary>
        public int embedResult = 0;
        /// <summary>
        /// 将指定的程序嵌入指定的控件
        /// </summary>
        private bool EmbedProcess(Process app,Control control)
        {
            // Get the main handle
            if (app == null || app.MainWindowHandle == IntPtr.Zero || control == null) return false;

            embedResult = 0;

            try
            {
                // Put it into this form
                embedResult = SetParent(app.MainWindowHandle, control.Handle);
                //if (embedResult != 0)
                //{ state = EmbedState.embeded; }
                //else
                //{ state = EmbedState.embedFailed; }
            }
            catch (Exception)
            { }
            try
            {
                // Remove border and whatnot               
                SetWindowLong(new HandleRef(this,app.MainWindowHandle), GWL_STYLE, WS_VISIBLE);
            }
            catch (Exception)
            { }
            try
            {
                // Move the window to overlay it on this window
                MoveWindow(app.MainWindowHandle, 0, 0, control.Width, control.Height, true);
            }
            catch (Exception)
            { }

            if (ShowEmbedResult)
            {
                var errorString = AppContainer.GetLastError();
                MessageBox.Show(errorString);
            }

            return (embedResult != 0);
        }

        public void NewMethod(Process app=null)
        {
            if (app == null) { app = this.m_AppProcess; }
            var c = Form.FromHandle(app.MainWindowHandle);
            var f = c as Form;
            if (f != null)
            {
                Console.WriteLine(f.Parent == null);
            }
        }

        /// <summary>
        /// Show a MessageBox to tell whether the embedding is successfully done.
        /// </summary>
        public bool ShowEmbedResult { get; set; }
    }
}
