using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZennMusic
{
    public enum SongRequestPayment
    {
        Piece, Ticket, Special
    }

    public class SongRequest
    {
        public string SongName { get; }
        public string UserName { get; }
        public SongRequestPayment Payment { get; }
        public bool isAccepted { get; private set; }

        public SongRequest(string songName, string userName, SongRequestPayment payment)
        {
            SongName = songName;
            UserName = userName;
            Payment = payment;
            isAccepted = false;
        }

        public bool AcceptRequest()
        {
            if (isAccepted)
                return false;

            isAccepted = true;
            return true;
        }
    }
}
