using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public partial class Enums
{
    public enum FromServerMessageType
    {
        LOGIN_SUCCESS = 0,
        LOGIN_FAIL = 1,
        USER_CONNECTED = 2,
        MOVE = 3,
        END_MOVE = 4,
        CREATE_ACCOUNT_FAIL = 5,
    }
}