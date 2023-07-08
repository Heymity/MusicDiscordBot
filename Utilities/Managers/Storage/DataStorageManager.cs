using DiscordBot.Utilities.Managers.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Timers;

namespace DiscordBot.Utilities.Managers.Storage
{
    [Serializable]
    public class DataStorageManager
    {
        public Dictionary<ulong, GuildDataManager> GuildsData { get; private set; }

        private static DataStorageManager _current;
        public static DataStorageManager Current
        {
            get => _current ??= new DataStorageManager();
            private set => _current = value;
        }

        public GuildDataManager this[ulong i]
        {
            get => GetOrCreateGuild(i);
            set
            {
                if (GuildsData.ContainsKey(i)) GuildsData[i] = value;
                else GuildsData.Add(i, value);
            }
        }

        public DataStorageManager()
        {
            GuildsData = new Dictionary<ulong, GuildDataManager>();
            _current = this;
            AutoSaveManager.Start();
        }

        public GuildDataManager GetOrCreateGuild(ulong guildId)
        {
            if (GuildsData.ContainsKey(guildId))
            {
                return GuildsData[guildId];
            }
            else
            {
                GuildsData.Add(guildId, new GuildDataManager(guildId));
                return GuildsData[guildId];
            }
        }

        public void SaveData()
        {
            FileStream fs = new FileStream("DataFile.dat", FileMode.Create);

            BinaryFormatter formatter = new BinaryFormatter();
            try
            {
                formatter.Serialize(fs, _current);
            }
            catch (SerializationException e)
            {
                Console.WriteLine("Failed to serialize. Reason: " + e.Message);
#if DEBUG
                throw;
#endif
            }
            finally
            {
                fs.Close();
            }
        }

        public void LoadData()
        {
            FileStream fs;
            if (!File.Exists("DataFile.dat")) fs = new FileStream("DataFile.dat", FileMode.Create);
            else fs = new FileStream("DataFile.dat", FileMode.Open);
            try
            {
                if (fs.Length == 0) return;
                BinaryFormatter formatter = new BinaryFormatter();

                _current = (DataStorageManager)formatter.Deserialize(fs);
            }
            catch (SerializationException e)
            {
                Console.WriteLine("Failed to deserialize. Reason: " + e.Message);
#if DEBUG
                throw;
#endif
            }
            finally
            {
                fs.Close();
            }
        }
    }

    public static class AutoSaveManager
    {
        private const double SECOND = 1000;
        private const double MINUTE = 60000;
        public const double SAVE_INTERVAL = 5 * MINUTE;

        public static Dictionary<ChangePriority, double> PriorityEffect { get; set; }

        public static Timer SaveSchedule { get; set; }
        public static Stopwatch ElapsedTime { get; set; }

        public static void Start()
        {
            ElapsedTime = new Stopwatch();
            SaveSchedule = new Timer()
            {
                Interval = SAVE_INTERVAL,
                AutoReset = true,
            };
            SaveSchedule.Elapsed += AutoSave;
            ElapsedTime.Start();
            SaveSchedule.Start();

            PriorityEffect = new Dictionary<ChangePriority, double>()
            {
                { ChangePriority.ImmediateSave, SAVE_INTERVAL },
                { ChangePriority.UserDataChange, 30 * SECOND },
                { ChangePriority.ImportantUserDataChange, MINUTE },
                { ChangePriority.ChannelDataChange, MINUTE },
                { ChangePriority.GuildDataChange, 2 * MINUTE },
                { ChangePriority.GeneralDataChange, 300 * SECOND }
            };
        }

        private static void AutoSave(object sender, ElapsedEventArgs e)
        {
            Console.WriteLine("Auto Saving...");
            DataStorageManager.Current.SaveData();
            ElapsedTime.Restart();
            SaveSchedule.Interval = SAVE_INTERVAL;
        }

        public static void ReduceIntervalByChangePriority(ChangePriority priority)
        {
            if (priority == 0) AutoSave(null, null);

            var priorityEffectSum = 0d;
            int forSize = (int)Math.Pow(2d, Enum.GetNames(typeof(ChangePriority)).Length);
            for (int baseBin = 0x0000_0001; baseBin < forSize; baseBin <<= 1)
            {
                if ((priority & (ChangePriority)baseBin) == (ChangePriority)baseBin)
                    priorityEffectSum += PriorityEffect[(ChangePriority)baseBin];
            }
            var remainingTime = SaveSchedule.Interval - ElapsedTime.ElapsedMilliseconds;
            var tmp = remainingTime - priorityEffectSum;
            if (tmp <= 0) AutoSave(null, null);
            else SaveSchedule.Interval -= priorityEffectSum + ElapsedTime.ElapsedMilliseconds;
        }
    }

    [Flags]
    public enum ChangePriority : int
    {
        ImmediateSave = 0,            // 0x0000_0000,
        UserDataChange = 1,           // 0x0000_0001,
        ImportantUserDataChange = 2,  // 0x0000_0010,
        ChannelDataChange = 4,        // 0x0000_0100,
        GuildDataChange = 8,          // 0x0000_1000,
        GeneralDataChange = 16,       // 0x0001_0000,
    }
}