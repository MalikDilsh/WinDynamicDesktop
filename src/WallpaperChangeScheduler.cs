﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace WinDynamicDesktop
{
    class WallpaperChangeScheduler
    {
        private int[] dayImages;
        private int[] nightImages;

        private bool isSunUp;
        private string lastDate = "yyyy-MM-dd";
        private int lastImageId = -1;
        private int lastImageNumber = -1;

        private WeatherData yesterdaysData;
        private WeatherData todaysData;
        private WeatherData tomorrowsData;

        public bool enableTransitions = true;
        public Timer wallpaperTimer = new Timer();

        public WallpaperChangeScheduler()
        {
            dayImages = JsonConfig.imageSettings.dayImageList;
            nightImages = JsonConfig.imageSettings.nightImageList;

            wallpaperTimer.Tick += new EventHandler(OnWallpaperTimerTick);
        }

        private string GetDateString(int todayDelta = 0)
        {
            DateTime date = DateTime.Today;

            if (todayDelta != 0)
            {
                date = date.AddDays(todayDelta);
            }

            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private WeatherData GetWeatherData(string dateStr)
        {
            WeatherData data = SunriseSunsetService.GetWeatherData(
                JsonConfig.settings.latitude, JsonConfig.settings.longitude, dateStr);

            return data;
        }

        private int GetImageNumber(DateTime startTime, TimeSpan timerLength)
        {
            TimeSpan elapsedTime = DateTime.Now - startTime;

            return (int)(elapsedTime.Ticks / timerLength.Ticks);
        }

        private void StartTimer(long tickInterval)
        {
            TimeSpan interval = new TimeSpan(tickInterval);

            wallpaperTimer.Interval = (int)interval.TotalMilliseconds;
            wallpaperTimer.Start();
        }

        private void SetWallpaper(int imageId)
        {
            string imageFilename = String.Format(JsonConfig.imageSettings.imageFilename, imageId);
            string imagePath = Path.Combine(Directory.GetCurrentDirectory(), "images", imageFilename);

            if (enableTransitions)
            {
                WallpaperChanger.EnableTransitions();
            }

            WallpaperChanger.SetWallpaper(imagePath);

            lastImageId = imageId;
        }

        public void StartScheduler(bool forceRefresh = false)
        {
            if (wallpaperTimer != null)
            {
                wallpaperTimer.Stop();
            }

            string currentDate = GetDateString();

            if (currentDate != lastDate || forceRefresh)
            {
                todaysData = GetWeatherData(currentDate);
                lastDate = currentDate;
            }

            if (DateTime.Now < todaysData.SunriseTime)
            {
                // Before sunrise
                yesterdaysData = GetWeatherData(GetDateString(-1));
                tomorrowsData = null;
            }
            else if (DateTime.Now > todaysData.SunsetTime)
            {
                // After sunset
                yesterdaysData = null;
                tomorrowsData = GetWeatherData(GetDateString(1));
            }
            else
            {
                // Between sunrise and sunset
                yesterdaysData = null;
                tomorrowsData = null;
            }

            lastImageId = -1;

            if (yesterdaysData == null && tomorrowsData == null)
            {
                StartDaySchedule();
            }
            else
            {
                StartNightSchedule();
            }
        }

        private void StartDaySchedule()
        {
            isSunUp = true;

            TimeSpan dayTime = todaysData.SunsetTime - todaysData.SunriseTime;
            TimeSpan timerLength = new TimeSpan(dayTime.Ticks / dayImages.Length);
            int imageNumber = GetImageNumber(todaysData.SunriseTime, timerLength);
            
            StartTimer(todaysData.SunriseTime.Ticks + timerLength.Ticks * (imageNumber + 1)
                - DateTime.Now.Ticks);

            if (dayImages[imageNumber] != lastImageId)
            {
                SetWallpaper(dayImages[imageNumber]);
                lastImageNumber = imageNumber;
            }
        }

        private void NextDayImage()
        {
            if (lastImageNumber == dayImages.Length - 1)
            {
                SwitchToNight();
                return;
            }

            TimeSpan dayTime = todaysData.SunsetTime - todaysData.SunriseTime;
            StartTimer(dayTime.Ticks / dayImages.Length);

            lastImageNumber++;
            SetWallpaper(dayImages[lastImageNumber]);
        }

        private void SwitchToNight()
        {
            tomorrowsData = GetWeatherData(GetDateString(1));
            lastImageNumber = -1;

            StartNightSchedule();
        }

        private void StartNightSchedule()
        {
            isSunUp = false;

            WeatherData day1Data = (yesterdaysData == null) ? todaysData : yesterdaysData;
            WeatherData day2Data = (yesterdaysData == null) ? tomorrowsData : todaysData;

            TimeSpan nightTime = day2Data.SunriseTime - day1Data.SunsetTime;
            TimeSpan timerLength = new TimeSpan(nightTime.Ticks / nightImages.Length);
            int imageNumber = GetImageNumber(day1Data.SunsetTime, timerLength);

            StartTimer(day1Data.SunsetTime.Ticks + timerLength.Ticks * (imageNumber + 1)
                - DateTime.Now.Ticks);

            if (nightImages[imageNumber] != lastImageId)
            {
                SetWallpaper(nightImages[imageNumber]);
                lastImageNumber = imageNumber;
            }
        }

        private void NextNightImage()
        {
            if (lastImageNumber == nightImages.Length - 1)
            {
                SwitchToDay();
                return;
            }

            WeatherData day1Data = (yesterdaysData == null) ? todaysData : yesterdaysData;
            WeatherData day2Data = (yesterdaysData == null) ? tomorrowsData : todaysData;

            TimeSpan nightTime = day2Data.SunriseTime - day1Data.SunsetTime;
            StartTimer(nightTime.Ticks / nightImages.Length);

            lastImageNumber++;
            SetWallpaper(nightImages[lastImageNumber]);

            if (DateTime.Now.Hour >= 0 && DateTime.Now.Hour < 12 && tomorrowsData != null)
            {
                yesterdaysData = todaysData;
                todaysData = tomorrowsData;
                tomorrowsData = null;
                lastDate = GetDateString();
            }
        }

        private void SwitchToDay()
        {
            yesterdaysData = null;
            lastImageNumber = -1;

            StartDaySchedule();
        }

        private void OnWallpaperTimerTick(object sender, EventArgs e)
        {
            wallpaperTimer.Stop();

            if (isSunUp)
            {
                NextDayImage();
            }
            else
            {
                NextNightImage();
            }
        }
    }
}
