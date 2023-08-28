using Discord.Interactions;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Platybot.Modules
{

    public class TicketModal : IModal
    {
        public string Title => "Create a Ticket";
        [InputLabel("Issue Description")]
        [ModalTextInput("TicketDescription", TextInputStyle.Paragraph, "Enter a short description of your issue here...", maxLength: 1000)]
        public string TicketDescription { get; set; }
    }
}
