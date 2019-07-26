// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Principal;

namespace Microsoft.AspNetCore.Authentication.Negotiate
{
    internal class ReflectedNegotiateState : INegotiateState
    {
        private static readonly ConstructorInfo _constructor;
        private static readonly MethodInfo _getOutgoingBlob;
        private static readonly MethodInfo _isCompleted;
        private static readonly MethodInfo _protocol;
        private static readonly MethodInfo _getIdentity;
        private static readonly MethodInfo _closeContext;

        private readonly object _instance;

        static ReflectedNegotiateState()
        {
            var ntAuthType = typeof(AuthenticationException).Assembly.GetType("System.Net.NTAuthentication");
            _constructor = ntAuthType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).First();
            _getOutgoingBlob = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("GetOutgoingBlob") && info.GetParameters().Count() == 2).Single();
            _isCompleted = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("get_IsCompleted")).Single();
            _protocol = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("get_ProtocolName")).Single();
            _closeContext = ntAuthType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(info =>
                info.Name.Equals("CloseContext")).Single();

            var negoStreamPalType = typeof(AuthenticationException).Assembly.GetType("System.Net.Security.NegotiateStreamPal");
            _getIdentity = negoStreamPalType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Where(info =>
                info.Name.Equals("GetIdentity")).Single();
        }

        public ReflectedNegotiateState()
        {
            // internal NTAuthentication(bool isServer, string package, NetworkCredential credential, string spn, ContextFlagsPal requestedContextFlags, ChannelBinding channelBinding)
            var credential = CredentialCache.DefaultCredentials;
            _instance = _constructor.Invoke(new object[] { true, "Negotiate", credential, null, 0, null });
        }

        // Copied rather than reflected to remove the IsCompleted -> CloseContext check.
        // The client doesn't need the context once auth is complete, but the server does.
        // I'm not sure why it auto-closes for the client given that the client closes it just a few lines later.
        // https://github.com/dotnet/corefx/blob/a3ab91e10045bb298f48c1d1f9bd5b0782a8ac46/src/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/AuthenticationHelper.NtAuth.cs#L134
        public string GetOutgoingBlob(string incomingBlob)
        {
            byte[] decodedIncomingBlob = null;
            if (incomingBlob != null && incomingBlob.Length > 0)
            {
                decodedIncomingBlob = Convert.FromBase64String(incomingBlob);
            }
            byte[] decodedOutgoingBlob = GetOutgoingBlob(decodedIncomingBlob, true);

            string outgoingBlob = null;
            if (decodedOutgoingBlob != null && decodedOutgoingBlob.Length > 0)
            {
                outgoingBlob = Convert.ToBase64String(decodedOutgoingBlob);
            }

            return outgoingBlob;
        }

        private byte[] GetOutgoingBlob(byte[] incomingBlob, bool thrownOnError)
        {
            return (byte[])_getOutgoingBlob.Invoke(_instance, new object[] { incomingBlob, thrownOnError });
        }

        public bool IsCompleted
        {
            get => (bool)_isCompleted.Invoke(_instance, Array.Empty<object>());
        }

        public string Protocol
        {
            get => (string)_protocol.Invoke(_instance, Array.Empty<object>());
        }

        public IIdentity GetIdentity()
        {
            return (IIdentity)_getIdentity.Invoke(obj: null, parameters: new object[] { _instance });
        }

        public void Dispose()
        {
            _closeContext.Invoke(_instance, Array.Empty<object>());
        }
    }
}
