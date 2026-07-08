using caelestia.Services;
using Microsoft.Data.Sqlite;
using NAudio.Wave;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Windows.ApplicationModel.VoiceCommands;

namespace caelestia
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window

    {
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(
        IntPtr hWnd,
        int id,
        uint fsModifiers,
        uint vk);
        //To remember the previous window to return focus after dashboard is toggled
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        private IntPtr previousWindow = IntPtr.Zero;
        private IntPtr dashBoardHandle;
        private readonly MediaPlayer dashBoardMusic = new();
        private AudioPlayer audioPlayer;
        private float currentVolume;
        private float[] spectrum = new float[48];
        private readonly float[] displayedSpectrum = new float[48];

        private readonly List<Rectangle> bars = new();
        private DispatcherTimer visualizerTimer = new();
        private Random random = new();

        private static readonly SolidColorBrush uiCrimson = new SolidColorBrush(Color.FromRgb(252,181,174));

        private bool isOpen = false;

        private DispatcherTimer slideTimer = new DispatcherTimer();

        private const double HiddenPosition = -490;
        private const double VisiblePosition = -7;

        private bool opening = false;

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(
        IntPtr hWnd,
        int id);

        private const int HOTKEY_ID = 9000;
        private const uint MOD_NONE = 0x0000;
        private const uint VK_OEM_3 = 0xC0;

        public MainWindow()
        {
            InitializeComponent();//constructor
            this.KeyDown += Hotkeys;//enables hotkeys to open apps
            LoadRandomWallpaper();
            Top = 30;
            Height = SystemParameters.PrimaryScreenHeight - Top - 10;
            Left = -460;
            dashBoardHandle = new WindowInteropHelper(this).Handle;

            //Open/Close positions
            Loaded += (s, e) =>
            {
                Left = -7;
            };

            //Border animations
            SolidColorBrush glowBrush = new SolidColorBrush(Color.FromRgb(110, 75, 250));
            glowBrush.BeginAnimation(
                SolidColorBrush.ColorProperty,
                new ColorAnimation
                {
                    From = Color.FromRgb(110, 75, 250),
                    To = Color.FromRgb(197, 141, 255),
                    Duration = TimeSpan.FromSeconds(15),
                    AutoReverse = true,
                    DecelerationRatio = 0.5,
                    AccelerationRatio = 0.5,                   
                    RepeatBehavior = RepeatBehavior.Forever
                });

            calendarCard.BorderBrush = glowBrush;
            profileCard.BorderBrush = glowBrush;
            animeCard.BorderBrush = glowBrush;
            bottomAnimeCard.BorderBrush = glowBrush;

            Koodo.BorderBrush = glowBrush;
            VSCode.BorderBrush = glowBrush;
            Zen.BorderBrush = glowBrush;
            Spotify.BorderBrush = glowBrush;

            ZenNotes.BorderBrush = glowBrush;
            button1.BorderBrush = glowBrush;
            button2.BorderBrush = glowBrush;


            hoYo.BorderBrush = glowBrush;
            gallery.BorderBrush = glowBrush;
            gimp.BorderBrush = glowBrush;
            visualStudio.BorderBrush = glowBrush;

            GenerateCalendar();
            SourceInitialized += MainWindow_SourceInitialized;

            slideTimer.Interval = TimeSpan.FromMilliseconds(0);
            slideTimer.Tick += SlideTimer_Tick;

            Deactivated += MainWindow_Deactivated;

            Random random = new Random();
            string[] images = Directory.GetFiles("Assets/Wallpapers");

            AnimeImage.Source = new BitmapImage(
                new Uri(images[random.Next(images.Length)],
                UriKind.RelativeOrAbsolute));

            //dashboard music
            string musicPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Audio", "columbinaLullaby.mp3");
            audioPlayer = new AudioPlayer(musicPath);
            audioPlayer.Volume = 0.5f;
            audioPlayer.VolumeChanged += AudioPlayer_VolumeChanged;


            dashBoardMusic.MediaEnded += (squirtle, eevee) =>
            {
                dashBoardMusic.Position = TimeSpan.Zero;
                dashBoardMusic.Play();
            };

            //dashboard cava
            for (int i = 0; i < 48; i++)
            {
                Rectangle bar = new Rectangle
                {
                    Width = Double.NaN,
                    Height = 10,
                    VerticalAlignment = VerticalAlignment.Bottom, //to make the bars grow from the bottom
                    Margin = new Thickness(2, 0, 2, 0),
                    Fill = Brushes.MediumPurple
                };

                VisualizerGrid.Children.Add(bar);


                bars.Add(bar);
            }
            for (int i = 0; i < spectrum.Length; i++)
            {
                Debug.Write($"{spectrum[i]:F3} ");
            }
            Debug.WriteLine("");
            Debug.WriteLine($"{spectrum[0]:F3} {spectrum[1]:F3} {spectrum[2]:F3} {spectrum[3]:F3} {spectrum[4]:F3}");
            Debug.WriteLine(bars.Count);

            visualizerTimer.Interval = TimeSpan.FromMilliseconds(30);
            visualizerTimer.Tick += VisualizerTimer_Tick;

            audioPlayer.SpectrumCalculated += AudioPlayer_SpectrumCalculated;


        }


        private void AudioPlayer_SpectrumCalculated(float[] data)
        {
            Array.Copy(data, spectrum, data.Length);
        }

        private void AudioPlayer_VolumeChanged(float volume)
        {
            currentVolume = volume/10;
        }
        //dashboard cava
        private void VisualizerTimer_Tick(object sender, EventArgs e)
        {
            // 1. Smooth out the raw spectrum values first
            for (int i = 0; i < spectrum.Length; i++)
            {
                displayedSpectrum[i] +=
                    (spectrum[i] - displayedSpectrum[i]) * 0.3f;
            }

            // 2. Loop through your 48 bars and apply the balancing weight
            for (int i = 0; i < bars.Count; i++)
            {
                // Calculate where this bar sits from 0.0 (far left) to 1.0 (far right)
                double progress = (double)i / (bars.Count - 1);

                // This curve starts low (0.08) for bass and goes to full strength (1.0) for treble
                double weight = 0.08 + (0.92 * Math.Pow(progress, 1.8));

                // Apply the weight directly to the spectrum value
                double balancedValue = displayedSpectrum[i] * weight;

                // Multiply the balanced value to get the visual height
                // (You may need to increase 600 to 1000+ now that the values are tamed!)
                double targetHeight = balancedValue *900;

                // Clamp the final height so it fits your UI limits smoothly
                targetHeight = Math.Clamp(targetHeight, 5, 55);

                DoubleAnimation animation = new()
                {
                    To = targetHeight,
                    Duration = TimeSpan.FromMilliseconds(16)
                };

                bars[i].BeginAnimation(Rectangle.HeightProperty, animation);
            }
        }


        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;

            int exStyle = GetWindowLong(hwnd, -20);

            SetWindowLong(hwnd, -20, exStyle | 0x80);
        }
        
        private void LoadCurrentBook()
        {
            string appData = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "koodo-reader", "uploads", "data", "config");
            string bookmarkDb = System.IO.Path.Combine(appData, "bookmarks.db");
            string booksDb = System.IO.Path.Combine(appData, "books.db");

            string bookKey = "";
            string percentage = "";
            string chapter = "";

            //Get latest bookmark
            using (var conn = new SqliteConnection($"Data Source={bookmarkDb}"))
            {
                conn.Open();

                var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT bookKey, percentage, chapter FROM bookmarks ORDER BY ROWID DESC LIMIT 1";

                using var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    bookKey = reader["bookKey"]?.ToString() ?? "";
                    percentage = reader["percentage"]?.ToString() ?? "";
                    chapter = reader["chapter"]?.ToString() ?? "";
                }
            }

            //Get latest Book Title
            using (var conn = new SqliteConnection($"Data Source={booksDb}"))
            {
                conn.Open();

                var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT name FROM books WHERE key = $key";

                cmd.Parameters.AddWithValue("$key", bookKey);

                double progress = Convert.ToDouble(percentage) * 100;
                string title = cmd.ExecuteScalar()?.ToString() ?? "Unknown Book";

                CurrentBookText.Text = $"📖 {title}";
                CurrentBookProgressText.Text = $"•{progress:F0}%•";
                CurrentBookChapterText.Text = $" {chapter} ";
            }
        }

        private void LoadRandomWallpaper()
        {
            string folder = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets",
                "Wallpapers");

            if (!Directory.Exists(folder))
                return;

            string[] images = Directory.GetFiles(folder);

            if (images.Length == 0)
                return;

            Random random = new Random();

            string selectedImage =
                images[random.Next(images.Length)];

            AnimeImage.Source =
                new BitmapImage(new Uri(selectedImage));
        }

        private void GenerateCalendar()
        {
            CalendarGrid.Children.Clear();

            DateTime now = DateTime.Now;

            MonthTitle.Text = now.ToString("MMMM yyyy");

            string[] days =
            {
        "S","M","T","W","T","F","S"
    };

            foreach (string day in days)
            {
                CalendarGrid.Children.Add(
                    new TextBlock
                    {
                        FontWeight = FontWeights.Bold,
                        Text = day,
                        Foreground = uiCrimson,
                        HorizontalAlignment = HorizontalAlignment.Center

                    });
            }

            DateTime firstDay =
                new DateTime(now.Year, now.Month, 1);

            int startDay =
                (int)firstDay.DayOfWeek;

            int daysInMonth =
                DateTime.DaysInMonth(now.Year, now.Month);

            for (int i = 0; i < startDay; i++)
            {
                CalendarGrid.Children.Add(
                    new TextBlock());
            }

            for (int day = 1; day <= daysInMonth; day++)
            {
                Border cell = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(2)
                };

                TextBlock text = new TextBlock
                {
                    Text = day.ToString(),
                    HorizontalAlignment =
                        HorizontalAlignment.Center,
                    VerticalAlignment =
                        VerticalAlignment.Center,
                    Foreground = Brushes.White
                };

                if (day == now.Day)
                {
                    cell.Background =
                        new SolidColorBrush(
                            Color.FromRgb(140, 110, 255));
                }

                cell.Child = text;

                CalendarGrid.Children.Add(cell);
            }
        }
        private void Hotkeys(object onyx, KeyEventArgs eevee)
        {   if (Keyboard.Modifiers == (ModifierKeys.Shift | ModifierKeys.Control) && eevee.Key == Key.Q)
            {
                Application.Current.Shutdown();
            }
            else if(isOpen==true) 
            {
                switch (eevee.Key)
                {
                    case Key.D1:
                        zenNotesLauncher();
                        ToggleDashboard();
                        break;
                    case Key.Q:
                        koodoLauncher();
                        ToggleDashboard();
                        break;
                    case Key.W:
                        vsCodeLaucher();
                        ToggleDashboard();
                        break;
                    case Key.E:
                        zenLauncher();
                        ToggleDashboard();
                        break;
                    case Key.R:
                        spotifyLauncher();
                        ToggleDashboard();
                        break;
                    case Key.A:
                        hoyoLauncher();
                        ToggleDashboard();
                        break;
                    case Key.S:
                        galleryLauncher();
                        ToggleDashboard();
                        break;
                    case Key.D:
                        gimpLauncher();
                        ToggleDashboard();
                        break;
                    case Key.F:
                        visualStudioLaucher();
                        ToggleDashboard();
                        break;
                    case Key.Escape:
                        ToggleDashboard();
                        break;
                    case Key.T:
                        terminalLauncher();
                        ToggleDashboard();
                        break;
                }
            }
        
        }

        //app launchers and app clicks
        private void terminalLauncher()
        {
            Process.Start(new ProcessStartInfo { FileName = @"C:\Program Files\WindowsApps\Microsoft.WindowsTerminal_1.24.11321.0_x64__8wekyb3d8bbwe\WindowsTerminal.exe" });
        }
        private void galleryLauncher()
        {
            Process.Start(new ProcessStartInfo { FileName = @"C:\Users\novap\Videos\Captures", UseShellExecute = true });
        }
   
        private void galleryClick(object onyx, RoutedEventArgs eevee)
        {
            galleryLauncher();
        }

        private void koodoLauncher()
        {
            Process.Start(new ProcessStartInfo { FileName = @"C:\Users\novap\AppData\Local\Programs\koodo-reader\Koodo Reader.exe", UseShellExecute = true });
        }
   
        private void koodoClick(Object onyx, RoutedEventArgs eevee)
        {
            koodoLauncher();
        }

        private void zenNotesLauncher()
        {
            Process.Start(new ProcessStartInfo { FileName = @"C:\Users\novap\AppData\Local\Programs\@zennotesdesktop\ZenNotes.exe" });
        }

        private void zenNotesClick(Object onyx, RoutedEventArgs eevee)
        {
            zenNotesLauncher();
        }

        private void vsCodeLaucher()
        {
            Process.Start(new ProcessStartInfo { FileName = @"C:\Users\novap\AppData\Local\Programs\Microsoft VS Code\Code.exe" });

        }
        private void vsCodeClick(Object onyx, RoutedEventArgs eevee)
        {
            vsCodeLaucher();
        }

        private void zenLauncher()
        {
            Process.Start(new ProcessStartInfo { FileName = @"C:\Program Files\Zen Browser\zen.exe" });

        }

        private void zenClick(Object onyx, RoutedEventArgs eevee)
        {
            zenLauncher();
        }
        
        private void spotifyLauncher()
        {
            Process.Start(new ProcessStartInfo { FileName = @"C:\Users\novap\AppData\Roaming\Spotify\Spotify.exe" });

        }

        private void spotifyClick(Object onyx, RoutedEventArgs eevee)
        {
            spotifyLauncher();
        }

        private void visualStudioLaucher()
        {
            Process.Start(new ProcessStartInfo { FileName = @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe" });

        }

        private void visualStudioClick(Object onyx, RoutedEventArgs eevee)
        {
            visualStudioLaucher();
        }
        private void gimpLauncher()
        {
            Process.Start(new ProcessStartInfo { FileName = @"C:\Users\novap\AppData\Local\Programs\GIMP 3\bin\gimp-3.exe" });

        }
        private void gimpClick(Object onyx, RoutedEventArgs eevee)
        {
            gimpLauncher();
        }

        private void hoyoLauncher()
        {
            Process.Start(new ProcessStartInfo { FileName = @"C:\Program Files\HoYoPlay\launcher.exe" });

        }

        private void hoyoClick(Object onyx, RoutedEventArgs eevee)
        {
            hoyoLauncher();
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            var helper = new WindowInteropHelper(this);

            RegisterHotKey(
                helper.Handle,
                HOTKEY_ID,
                MOD_NONE,
                VK_OEM_3);

            HwndSource source =
                HwndSource.FromHwnd(helper.Handle);

            source.AddHook(HwndHook);
        }

        private IntPtr HwndHook(
            IntPtr hwnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam,
            ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY &&
                wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleDashboard();

                handled = true;
            }

            return IntPtr.Zero;
        }

        private void ToggleDashboard()
        {
            double target = isOpen ? HiddenPosition : VisiblePosition;


            DoubleAnimation animation = new DoubleAnimation
            {
                To = target,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            BeginAnimation(Window.LeftProperty, animation);

            if (!isOpen)
            {   
                previousWindow = GetForegroundWindow();
                if(previousWindow == dashBoardHandle)
                {
                    previousWindow = IntPtr.Zero;
                }
                Activate();
                Focus();
                audioPlayer.Play();
                LoadCurrentBook();
                visualizerTimer.Start();
            }
            else
            {
                audioPlayer.Pause();
                visualizerTimer.Stop();
                if(previousWindow != IntPtr.Zero) {
                    SetForegroundWindow(previousWindow);
                }
            }

                isOpen = !isOpen;
            
        }

        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            if (isOpen && !slideTimer.IsEnabled)
            {
                ToggleDashboard();
            }
        }

        private void SlideTimer_Tick(object? sender, EventArgs e)
        {
            if (opening)
            {
                Left += 10;

                if (Left >= VisiblePosition)
                {
                    Left = VisiblePosition;
                    slideTimer.Stop();
                }
            }
            else
            {
                Left -= 10;

                if (Left <= HiddenPosition)
                {
                    Left = HiddenPosition;
                    slideTimer.Stop();
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            UnregisterHotKey(
                new WindowInteropHelper(this).Handle,
                HOTKEY_ID);

            base.OnClosed(e);
        }
    }
}