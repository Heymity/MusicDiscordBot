# Discord Bot Template for C#
This is a template for making a discord bot using C#, it is based on the discord-net/discord.net framework. It comes with some useful features and data structures is order to make easier creating a new discord bot in C#.

## What are the features
 - Command Handler with a per guild(server) change prefix command built in
 - A data structure for saving data related to a certain guild or channel
 - A auto-save
 - A web request helper
 - A automatically generated help message that includes all commands of the bot even the ones added after creating the bot from this template.

----------

## The saving data structure

It is composed by 3 classes for now, DataStorageManager, GuildDataManager and ChannelDataManager. The DataStorageManager it the class that is actually serialized and then saved. It contains basically one field, a ```Dictionary<ulong, GuildDataManager>``` in which the ulong is the guilds ids and the GuildDataManager is the correspondig data for that guild id.

The GuildDataManager stores the command prefix, a ```Dictionary<ulong, ChannelDataManager>``` in which the ulong is the channel ids and the ChannelDataManager is the correspondig data for that channel id, and any other data you wish to store per guild

The ChannelDataManager stores any data you want to save per channel. By default it comes with nothing on it.

## How to use it: 

The DataStorageManager class has a singleton, access it and use the indexer property to access the guild related stuff and so on.

```c#
DataStorageManager.Current[GUILD ID].ChannelsData[CHANNEL ID]
```

----------

## The auto-save feature
This feature essentially saves the current state of the bot, stored in the DataStorageManager class, after a determinated amount of time, which can be easily changed in the AutoSaveManager class, located on the same file from the DataStorageManager.
```c#
public static class AutoSaveManager
{
    private const double SECOND = 1000;
    private const double MINUTE = 60000;
    public const double SAVE_INTERVAL = 5 * MINUTE; // <- Change here
    [...]

```
It also has a feature that can be used when making your commands: in case your command change something important regarding the state of the bot, it can decrease the time until the next change by some amount, depending on the priority of the change. This amounts of time to be subtracted can be easily changed on the AutoSaveManager class, and the priorities can also be easily changed on the ChangePriority enum located at the same file, but when changing this enum you must keep the ImmediateSave flag and the flags pattern (each item of the array is equal to 2 ^ its position on the enum, starting after ImmediateSave which is always 0).

## Changing the amounts of time:

```c#
public static void Start()
{
    [...]
    PriorityEffect = new Dictionary<ChangePriority, double>()
    {
        //                       v  Change here the values  v
        { ChangePriority.ImmediateSave, SAVE_INTERVAL },
        { ChangePriority.UserDataChange, 30 * SECOND },
        { ChangePriority.ImportantUserDataChange, MINUTE },
        { ChangePriority.ChannelDataChange, MINUTE },
        { ChangePriority.GuildDataChange, 2 * MINUTE },
        { ChangePriority.GeneralDataChange, 300 * SECOND }
    };
}

```

## Changing the ChangePriority enum:

If you pay attention to the code below, you will notice that the values there are all 1 << (position of the name), or in normal decimal digits 2 ^ (position of the name), this geometric progression must be kept.

In case you are not familiar with the Enum Flags attribute, it allows you to merge the enums values, for example you can use ChangePriority.UserDataChange | ChangePriority.GuildDataChange and the code will subtract the amount of time from both enum flags. for more info check [this documentation](https://docs.microsoft.com/pt-br/dotnet/api/system.flagsattribute?view=net-5.0)

```c#
[Flags]
public enum ChangePriority : int
{
    ImmediateSave = 0,            // 0x0000_0000,  NEVER CHANGE THIS ONE
    // CHANGE FROM HERE LOWER and keep the geometric progression (1 2 4 8 16 ...)
    UserDataChange = 1,           // 0x0000_0001,
    ImportantUserDataChange = 2,  // 0x0000_0010,
    ChannelDataChange = 4,        // 0x0000_0100,
    GuildDataChange = 8,          // 0x0000_1000,
    GeneralDataChange = 16,       // 0x0001_0000,
}
```
