using DSharpPlus;
using DSharpPlus.SlashCommands;
using DSharpPlus.Interactivity;
using DSharpPlus.VoiceNext;
using DSharpPlus.Lavalink;
using DSharpPlus.Entities;
using Timer = System.Timers.Timer;
using Topshelf;

namespace ImaginationCreations
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var rc = HostFactory.Run(x =>                                 
            {
                x.Service<BotService>(s =>                                
                {
                    s.ConstructUsing(name => new BotService());           
                    s.WhenStarted(tc => tc.Start());                      
                    s.WhenStopped(tc => tc.Stop());                       
                });
                x.RunAsLocalSystem();                                     

                x.SetDescription("Imagination Creations Bot");            
                x.SetDisplayName("IC Discord Bot");                       
                x.SetServiceName("ICDiscordBot");                         
            });                                                           

            var exitCode = (int)Convert.ChangeType(rc, rc.GetTypeCode()); 
            Environment.ExitCode = exitCode;
        }
    }

    public class BotService
    {
        readonly Timer _timer;
        public BotService()
        {
            _timer = new Timer();

            Bot bot = new Bot();
            bot.RunAsync();
        }
        public void Start() { _timer.Start(); }
        public void Stop() { _timer.Stop(); }
    }
}