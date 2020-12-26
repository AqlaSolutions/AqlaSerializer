// Modified by Vladyslav Taranov for AqlaSerializer, 2016
// TODO for NETSTANDARD ProtoEndpointBehavior must be added with client.Endpoint.EndpointBehaviors.Add(endpoint);
#if FEAT_SERVICEMODEL && PLAT_XMLSERIALIZER && !NETSTANDARD
using System;
using System.ServiceModel.Configuration;

namespace AqlaSerializer.ServiceModel
{
    /// <summary>
    /// Configuration element to swap out DatatContractSerilaizer with the XmlProtoSerializer for a given endpoint.
    /// </summary>
    /// <seealso cref="ProtoEndpointBehavior"/>
    public class ProtoBehaviorExtension : BehaviorExtensionElement
    {
        /// <summary>
        /// Gets the type of behavior.
        /// </summary>     
        public override Type BehaviorType => typeof(ProtoEndpointBehavior);

        /// <summary>
        /// Creates a behavior extension based on the current configuration settings.
        /// </summary>
        /// <returns>The behavior extension.</returns>
        protected override object CreateBehavior()
        {
            return new ProtoEndpointBehavior();
        }
    }
}
#endif