
using System;

namespace Script.NetDriver
{
    public class WRpc
    {
        private string Uri;
        
        public WRpc(string uri)
        {
            this.Uri = uri;
        }

        public void Notify<T>()
        {
            
        }
    }
}