using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Cohere.Domain.Service.FCM.Exceptions
{
    public class FCMUnauthorizedException : FCMException
    {
        public FCMUnauthorizedException() : base(HttpStatusCode.Unauthorized, "Unauthorized")
        {

        }
    }
}
