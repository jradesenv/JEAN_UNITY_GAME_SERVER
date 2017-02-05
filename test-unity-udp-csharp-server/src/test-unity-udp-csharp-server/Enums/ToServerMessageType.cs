using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public partial class Enums
{
    public enum ToServerMessageType
    {
        LOGIN = 0,
        MOVE = 1,
        END_MOVE = 2,
        GET_OTHER_PLAYERS = 3,
        CREATE_ACCOUNT = 4
    }
}
