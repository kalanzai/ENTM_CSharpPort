﻿/* ***************************************************************************
 * This file is part of SharpNEAT - Evolution of Neural Networks.
 * 
 * Copyright 2004-2006, 2009-2010 Colin Green (sharpneat@gmail.com)
 *
 * SharpNEAT is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * SharpNEAT is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with SharpNEAT.  If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Xml;
using SharpNeat.Decoders;
using SharpNeat.EvolutionAlgorithms.ComplexityRegulation;
using System.Linq;
using System.Threading;
using SharpNeat.Decoders.HyperNeat;
using SharpNeat.Network;

namespace SharpNeat.Domains
{
    /// <summary>
    /// Static helper methods for experiment initialization.
    /// </summary>
    public static class ExperimentUtils
    {
        /// <summary>
        /// Create a network activation scheme from the scheme setting in the provided config XML.
        /// </summary>
        /// <returns></returns>
        public static NetworkActivationScheme CreateActivationScheme(XmlElement xmlConfig, string activationElemName)
        {
            // Get root activation element.
            XmlNodeList nodeList = xmlConfig.GetElementsByTagName(activationElemName, "");
            if (nodeList.Count != 1) {
                throw new ArgumentException("Missing or invalid activation XML config setting.");
            }

            XmlElement xmlActivation = nodeList[0] as XmlElement;
            string schemeStr = XmlUtils.TryGetValueAsString(xmlActivation, "Scheme");
            switch (schemeStr)
            {
                case "Acyclic":
                    return NetworkActivationScheme.CreateAcyclicScheme();
                case "CyclicFixedIters":
                    int iters = XmlUtils.GetValueAsInt(xmlActivation, "Iters");
                    return NetworkActivationScheme.CreateCyclicFixedTimestepsScheme(iters);
                case "CyclicRelax":
                    double deltaThreshold = XmlUtils.GetValueAsDouble(xmlActivation, "Threshold");
                    int maxIters = XmlUtils.GetValueAsInt(xmlActivation, "MaxIters");
                    return NetworkActivationScheme.CreateCyclicRelaxingActivationScheme(deltaThreshold, maxIters);
            }
            throw new ArgumentException(string.Format("Invalid or missing ActivationScheme XML config setting [{0}]", schemeStr));
        }

        /// <summary>
        /// Create a complexity regulation strategy based on the provided XML config values.
        /// </summary>
        public static IComplexityRegulationStrategy CreateComplexityRegulationStrategy(XmlElement xmlConfig, string complexityElemName)
        {
            // Get root activation element.
            XmlNodeList nodeList = xmlConfig.GetElementsByTagName(complexityElemName, "");
            if (nodeList.Count != 1)
            {
                throw new ArgumentException("Missing or invalid complexity XML config setting.");
            }

            XmlElement xmlComplexity = nodeList[0] as XmlElement;

            string complexityRegulationStr = XmlUtils.TryGetValueAsString(xmlComplexity, "ComplexityRegulationStrategy");
            int? complexityThreshold = XmlUtils.TryGetValueAsInt(xmlComplexity, "ComplexityThreshold");

            ComplexityCeilingType ceilingType;
            if (!Enum.TryParse<ComplexityCeilingType>(complexityRegulationStr, out ceilingType)) {
                return new NullComplexityRegulationStrategy();
            }

            if (null == complexityThreshold) {
                throw new ArgumentNullException("threshold", string.Format("threshold must be provided for complexity regulation strategy type [{0}]", ceilingType));
            }

            return new DefaultComplexityRegulationStrategy(ceilingType, complexityThreshold.Value);
        }

        /// <summary>
        /// Read Parallel Extensions options from config XML.
        /// </summary>
        /// <param name="xmlConfig"></param>
        /// <returns></returns>
        public static ParallelOptions ReadParallelOptions(XmlElement xmlConfig)
        {
            // Get parallel options.
            ParallelOptions parallelOptions;
            int? maxDegreeOfParallelism = XmlUtils.TryGetValueAsInt(xmlConfig, "MaxDegreeOfParallelism");
            if (null != maxDegreeOfParallelism) {
                parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism.Value };
            } else {
                parallelOptions = new ParallelOptions();
            }
            return parallelOptions;
        }

        /// <summary>
        /// Read Radial Basis Function settings from config XML.
        /// </summary>
        public static void ReadRbfAuxArgMutationConfig(XmlElement xmlConfig, out double mutationSigmaCenter, out double mutationSigmaRadius)
        {
            // Get root activation element.
            XmlNodeList nodeList = xmlConfig.GetElementsByTagName("RbfAuxArgMutationConfig", "");
            if (nodeList.Count != 1) {
                throw new ArgumentException("Missing or invalid RbfAuxArgMutationConfig XML config settings.");
            }

            XmlElement xmlRbfConfig = nodeList[0] as XmlElement;
            double? center = XmlUtils.TryGetValueAsDouble(xmlRbfConfig, "MutationSigmaCenter");
            double? radius = XmlUtils.TryGetValueAsDouble(xmlRbfConfig, "MutationSigmaRadius");
            if (null == center || null == radius)
            {
                throw new ArgumentException("Missing or invalid RbfAuxArgMutationConfig XML config settings.");
            }

            mutationSigmaCenter = center.Value;
            mutationSigmaRadius = radius.Value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="substrateXml"></param>
        /// <param name="substrateSettingsXml"></param>
        /// <param name="multispatial"></param>
        /// <returns></returns>
        public static ISubstrate ReadSubstrateFromXml(XmlElement substrateXml, XmlElement substrateSettingsXml)
        {
            var activationFunction = CreateActivationFunctionFromString(XmlUtils.GetValueAsString(substrateSettingsXml, "Function"));
            var weightThreshold = XmlUtils.TryGetValueAsDouble(substrateSettingsXml, "WeightThreshold") ?? 0.2;
            var maxWeight = XmlUtils.TryGetValueAsDouble(substrateSettingsXml, "MaxWeight") ?? 5.0;
            bool multispatial = XmlUtils.TryGetValueAsBool(substrateSettingsXml, "Multispatial") ?? false;
            bool leo = XmlUtils.TryGetValueAsBool(substrateSettingsXml, "Leo") ?? false;

            var layerlist = new List<SubstrateNodeSet>();
            var nodes = new Dictionary<uint, SubstrateNode>();
            uint nodeid = 1;
            foreach (XmlElement layer in substrateXml.GetElementsByTagName("Layer"))
            {
                SubstrateNodeSet.LayerType type;
                if(!Enum.TryParse(layer.GetAttribute("type"), out type))
                    throw new Exception("Layer type must be defined as Input/Output/Hidden");
                
                var tmp = new SubstrateNodeSet(layer.ChildNodes.Count, type);
                foreach (XmlNode n in layer.ChildNodes)
                {
                    if (n is XmlComment)
                    {
                        continue;
                    }
                    var node = (XmlElement) n;
                    var tmpNode = new SubstrateNode(nodeid, Array.ConvertAll(node.InnerText.Split(','), double.Parse));
                    tmp.NodeList.Add(tmpNode);
                    nodes.Add(nodeid, tmpNode);
                    nodeid++;
                }    
                layerlist.Add(tmp);
            }

            XmlNodeList mappings = substrateXml.GetElementsByTagName("Mapping");
            XmlNodeList connections = substrateXml.GetElementsByTagName("Connection");

            ISubstrate retval;
            if (connections.Count > 0)
            {
                var connectionList = new List<SubstrateConnection>();
                foreach (XmlElement connection in connections)
                {
                    var ids = Array.ConvertAll(connection.InnerText.Split(','), uint.Parse);
                    connectionList.Add(new SubstrateConnection(nodes[ids[0]], nodes[ids[1]]));
                }

                retval = multispatial ? (ISubstrate) new MultiSpatialSubstrate(layerlist, activationFunction, 0,
                    weightThreshold, maxWeight, connectionList) : new Substrate(layerlist, activationFunction, 0,
                    weightThreshold, maxWeight, connectionList);
            }
            else if (mappings.Count > 0)
            {
                var mappingList = new List<NodeSetMapping>();
                foreach (XmlElement mapping in mappings)
                {
                    var ids = Array.ConvertAll(mapping.InnerText.Split(','), int.Parse);
                    double maxDist;
                    double? maxDistN = null;
                    if (double.TryParse(mapping.GetAttribute("maxDist"), out maxDist))
                        maxDistN = maxDist;

                    mappingList.Add(NodeSetMapping.Create(ids[0], ids[1], maxDistN));
                }
                retval = multispatial ? (ISubstrate) new MultiSpatialSubstrate(layerlist, activationFunction, 0,
                    weightThreshold, maxWeight, mappingList) : new Substrate(layerlist, activationFunction, 0,
                    weightThreshold, maxWeight, mappingList);
            }
            else
            {
                throw new XmlException("Faulty substrate definition, at least one Mapping or Connection element must be defined.");
            }
            retval.Leo = leo;
            return retval;
        }

        private static IActivationFunctionLibrary CreateActivationFunctionFromString(string name)
        {
            name = name.ToLower(CultureInfo.InvariantCulture);
            switch (name)
            {
                case "absolute":
                    return DefaultActivationFunctionLibrary.CreateLibraryNeat(Absolute.__DefaultInstance);
                case "absoluteroot":
                    return DefaultActivationFunctionLibrary.CreateLibraryNeat(AbsoluteRoot.__DefaultInstance);
                case "gaussian":
                    return DefaultActivationFunctionLibrary.CreateLibraryNeat(Gaussian.__DefaultInstance);
                case "inverseabsolutesigmoid":
                    return DefaultActivationFunctionLibrary.CreateLibraryNeat(InverseAbsoluteSigmoid.__DefaultInstance);
                case "plainsigmoid":
                    return DefaultActivationFunctionLibrary.CreateLibraryNeat(PlainSigmoid.__DefaultInstance);
                case "steepenedsigmoid":
                    return DefaultActivationFunctionLibrary.CreateLibraryNeat(SteepenedSigmoid.__DefaultInstance);
                case "steepenedsigmoidapproximation":
                    return
                        DefaultActivationFunctionLibrary.CreateLibraryNeat(
                            SteepenedSigmoidApproximation.__DefaultInstance);
                case "stepfunction":
                    return DefaultActivationFunctionLibrary.CreateLibraryNeat(StepFunction.__DefaultInstance);
                case "linear":
                    return DefaultActivationFunctionLibrary.CreateLibraryNeat(Linear.__DefaultInstance);
                default:
                    throw new XmlException("Invalid substrate configuration: 'Function'");
            }
        }
    }    
}
