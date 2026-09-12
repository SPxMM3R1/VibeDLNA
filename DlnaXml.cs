using System.Net;
using System.Xml.Linq;

namespace FolderDlnaServer;

internal static class DlnaXml
{
    private const string ContentDirectoryService = "urn:schemas-upnp-org:service:ContentDirectory:1";
    private const string ConnectionManagerService = "urn:schemas-upnp-org:service:ConnectionManager:1";

    public static string DeviceDescription(string friendlyName, string uuid)
    {
        var escapedName = WebUtility.HtmlEncode(friendlyName);
        return $"""
            <?xml version="1.0"?>
            <root xmlns="urn:schemas-upnp-org:device-1-0">
              <specVersion>
                <major>1</major>
                <minor>0</minor>
              </specVersion>
              <device>
                <deviceType>urn:schemas-upnp-org:device:MediaServer:1</deviceType>
                <friendlyName>{escapedName}</friendlyName>
                <manufacturer>VibeDLNA</manufacturer>
                <manufacturerURL>https://openai.com</manufacturerURL>
                <modelDescription>Servidor DLNA local para una carpeta de Windows</modelDescription>
                <modelName>VibeDLNA Server</modelName>
                <modelNumber>1.0</modelNumber>
                <serialNumber>{uuid}</serialNumber>
                <UDN>uuid:{uuid}</UDN>
                <presentationURL>/</presentationURL>
                <serviceList>
                  <service>
                    <serviceType>{ContentDirectoryService}</serviceType>
                    <serviceId>urn:upnp-org:serviceId:ContentDirectory</serviceId>
                    <SCPDURL>/ContentDirectory/scpd.xml</SCPDURL>
                    <controlURL>/ContentDirectory/control</controlURL>
                    <eventSubURL>/ContentDirectory/event</eventSubURL>
                  </service>
                  <service>
                    <serviceType>{ConnectionManagerService}</serviceType>
                    <serviceId>urn:upnp-org:serviceId:ConnectionManager</serviceId>
                    <SCPDURL>/ConnectionManager/scpd.xml</SCPDURL>
                    <controlURL>/ConnectionManager/control</controlURL>
                    <eventSubURL>/ConnectionManager/event</eventSubURL>
                  </service>
                </serviceList>
              </device>
            </root>
            """;
    }

    public static string ContentDirectoryScpd() => """
        <?xml version="1.0"?>
        <scpd xmlns="urn:schemas-upnp-org:service-1-0">
          <specVersion><major>1</major><minor>0</minor></specVersion>
          <actionList>
            <action>
              <name>GetSearchCapabilities</name>
              <argumentList>
                <argument><name>SearchCaps</name><direction>out</direction><relatedStateVariable>SearchCapabilities</relatedStateVariable></argument>
              </argumentList>
            </action>
            <action>
              <name>GetSortCapabilities</name>
              <argumentList>
                <argument><name>SortCaps</name><direction>out</direction><relatedStateVariable>SortCapabilities</relatedStateVariable></argument>
              </argumentList>
            </action>
            <action>
              <name>GetSystemUpdateID</name>
              <argumentList>
                <argument><name>Id</name><direction>out</direction><relatedStateVariable>SystemUpdateID</relatedStateVariable></argument>
              </argumentList>
            </action>
            <action>
              <name>Browse</name>
              <argumentList>
                <argument><name>ObjectID</name><direction>in</direction><relatedStateVariable>A_ARG_TYPE_ObjectID</relatedStateVariable></argument>
                <argument><name>BrowseFlag</name><direction>in</direction><relatedStateVariable>A_ARG_TYPE_BrowseFlag</relatedStateVariable></argument>
                <argument><name>Filter</name><direction>in</direction><relatedStateVariable>A_ARG_TYPE_Filter</relatedStateVariable></argument>
                <argument><name>StartingIndex</name><direction>in</direction><relatedStateVariable>A_ARG_TYPE_Index</relatedStateVariable></argument>
                <argument><name>RequestedCount</name><direction>in</direction><relatedStateVariable>A_ARG_TYPE_Count</relatedStateVariable></argument>
                <argument><name>SortCriteria</name><direction>in</direction><relatedStateVariable>A_ARG_TYPE_SortCriteria</relatedStateVariable></argument>
                <argument><name>Result</name><direction>out</direction><relatedStateVariable>A_ARG_TYPE_Result</relatedStateVariable></argument>
                <argument><name>NumberReturned</name><direction>out</direction><relatedStateVariable>A_ARG_TYPE_Count</relatedStateVariable></argument>
                <argument><name>TotalMatches</name><direction>out</direction><relatedStateVariable>A_ARG_TYPE_Count</relatedStateVariable></argument>
                <argument><name>UpdateID</name><direction>out</direction><relatedStateVariable>A_ARG_TYPE_UpdateID</relatedStateVariable></argument>
              </argumentList>
            </action>
          </actionList>
          <serviceStateTable>
            <stateVariable sendEvents="yes"><name>SystemUpdateID</name><dataType>ui4</dataType></stateVariable>
            <stateVariable sendEvents="no"><name>SearchCapabilities</name><dataType>string</dataType></stateVariable>
            <stateVariable sendEvents="no"><name>SortCapabilities</name><dataType>string</dataType></stateVariable>
            <stateVariable sendEvents="no"><name>A_ARG_TYPE_ObjectID</name><dataType>string</dataType></stateVariable>
            <stateVariable sendEvents="no"><name>A_ARG_TYPE_Result</name><dataType>string</dataType></stateVariable>
            <stateVariable sendEvents="no"><name>A_ARG_TYPE_SearchCriteria</name><dataType>string</dataType></stateVariable>
            <stateVariable sendEvents="no"><name>A_ARG_TYPE_BrowseFlag</name><dataType>string</dataType><allowedValueList><allowedValue>BrowseMetadata</allowedValue><allowedValue>BrowseDirectChildren</allowedValue></allowedValueList></stateVariable>
            <stateVariable sendEvents="no"><name>A_ARG_TYPE_Filter</name><dataType>string</dataType></stateVariable>
            <stateVariable sendEvents="no"><name>A_ARG_TYPE_SortCriteria</name><dataType>string</dataType></stateVariable>
            <stateVariable sendEvents="no"><name>A_ARG_TYPE_Index</name><dataType>ui4</dataType></stateVariable>
            <stateVariable sendEvents="no"><name>A_ARG_TYPE_Count</name><dataType>ui4</dataType></stateVariable>
            <stateVariable sendEvents="no"><name>A_ARG_TYPE_UpdateID</name><dataType>ui4</dataType></stateVariable>
          </serviceStateTable>
        </scpd>
        """;

    public static string ConnectionManagerScpd() => """
        <?xml version="1.0"?>
        <scpd xmlns="urn:schemas-upnp-org:service-1-0">
          <specVersion><major>1</major><minor>0</minor></specVersion>
          <actionList>
            <action>
              <name>GetProtocolInfo</name>
              <argumentList>
                <argument><name>Source</name><direction>out</direction><relatedStateVariable>SourceProtocolInfo</relatedStateVariable></argument>
                <argument><name>Sink</name><direction>out</direction><relatedStateVariable>SinkProtocolInfo</relatedStateVariable></argument>
              </argumentList>
            </action>
            <action>
              <name>GetCurrentConnectionIDs</name>
              <argumentList>
                <argument><name>ConnectionIDs</name><direction>out</direction><relatedStateVariable>CurrentConnectionIDs</relatedStateVariable></argument>
              </argumentList>
            </action>
            <action>
              <name>GetCurrentConnectionInfo</name>
              <argumentList>
                <argument><name>ConnectionID</name><direction>in</direction><relatedStateVariable>A_ARG_TYPE_ConnectionID</relatedStateVariable></argument>
                <argument><name>RcsID</name><direction>out</direction><relatedStateVariable>A_ARG_TYPE_RcsID</relatedStateVariable></argument>
                <argument><name>AVTransportID</name><direction>out</direction><relatedStateVariable>A_ARG_TYPE_AVTransportID</relatedStateVariable></argument>
                <argument><name>ProtocolInfo</name><direction>out</direction><relatedStateVariable>A_ARG_TYPE_ProtocolInfo</relatedStateVariable></argument>
                <argument><name>PeerConnectionManager</name><direction>out</direction><relatedStateVariable>A_ARG_TYPE_ConnectionManager</relatedStateVariable></argument>
                <argument><name>PeerConnectionID</name><direction>out</direction><relatedStateVariable>A_ARG_TYPE_ConnectionID</relatedStateVariable></argument>
                <argument><name>Direction</name><direction>out</direction><relatedStateVariable>A_ARG_TYPE_Direction</relatedStateVariable></argument>
                <argument><name>Status</name><direction>out</direction><relatedStateVariable>A_ARG_TYPE_ConnectionStatus</relatedStateVariable></argument>
              </argumentList>
            </action>
          </actionList>
          <serviceStateTable>
            <stateVariable sendEvents="yes"><name>SourceProtocolInfo</name><dataType>string</dataType></stateVariable>
            <stateVariable sendEvents="yes"><name>SinkProtocolInfo</name><dataType>string</dataType></stateVariable>
            <stateVariable sendEvents="yes"><name>CurrentConnectionIDs</name><dataType>string</dataType></stateVariable>
            <stateVariable sendEvents="no"><name>A_ARG_TYPE_ConnectionStatus</name><dataType>string</dataType><allowedValueList><allowedValue>OK</allowedValue><allowedValue>ContentFormatMismatch</allowedValue><allowedValue>InsufficientBandwidth</allowedValue><allowedValue>UnreliableChannel</allowedValue><allowedValue>Unknown</allowedValue></allowedValueList></stateVariable>
            <stateVariable sendEvents="no"><name>A_ARG_TYPE_ConnectionManager</name><dataType>string</dataType></stateVariable>
            <stateVariable sendEvents="no"><name>A_ARG_TYPE_Direction</name><dataType>string</dataType><allowedValueList><allowedValue>Input</allowedValue><allowedValue>Output</allowedValue></allowedValueList></stateVariable>
            <stateVariable sendEvents="no"><name>A_ARG_TYPE_ProtocolInfo</name><dataType>string</dataType></stateVariable>
            <stateVariable sendEvents="no"><name>A_ARG_TYPE_ConnectionID</name><dataType>i4</dataType></stateVariable>
            <stateVariable sendEvents="no"><name>A_ARG_TYPE_AVTransportID</name><dataType>i4</dataType></stateVariable>
            <stateVariable sendEvents="no"><name>A_ARG_TYPE_RcsID</name><dataType>i4</dataType></stateVariable>
          </serviceStateTable>
        </scpd>
        """;

    public static string BuildDidl(
        IEnumerable<DlnaEntry> entries,
        string baseUrl,
        ThumbnailCache? thumbnailCache = null)
    {
        XNamespace didl = "urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/";
        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        XNamespace upnp = "urn:schemas-upnp-org:metadata-1-0/upnp/";

        var root = new XElement(didl + "DIDL-Lite",
            new XAttribute(XNamespace.Xmlns + "dc", dc.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "upnp", upnp.NamespaceName));

        foreach (var entry in entries)
        {
            if (entry.IsDirectory)
            {
                root.Add(new XElement(didl + "container",
                    new XAttribute("id", entry.Id),
                    new XAttribute("parentID", entry.ParentId),
                    new XAttribute("restricted", "1"),
                    new XAttribute("searchable", "0"),
                    new XAttribute("childCount", entry.ChildCount),
                    new XElement(dc + "title", entry.Title),
                    new XElement(upnp + "class", "object.container.storageFolder")));
            }
            else
            {
                var mediaUrl = $"{baseUrl}/media/{Uri.EscapeDataString(entry.Id)}/{Uri.EscapeDataString(Path.GetFileName(entry.FullPath))}";
                var item = new XElement(didl + "item",
                    new XAttribute("id", entry.Id),
                    new XAttribute("parentID", entry.ParentId),
                    new XAttribute("restricted", "1"),
                    new XElement(dc + "title", entry.Title),
                    new XElement(upnp + "class", entry.UpnpClass),
                    new XElement(didl + "res",
                        new XAttribute("protocolInfo", MediaTypes.GetProtocolInfo(entry.MimeType)),
                        new XAttribute("size", entry.Size),
                        mediaUrl));

                if (thumbnailCache is not null
                    && MediaTypes.TryGet(entry.FullPath, out var mediaType)
                    && mediaType.Kind == MediaKind.Video
                    && thumbnailCache.TryGetCached(entry.FullPath) is { } thumbnailKey)
                {
                    item.Add(new XElement(
                        upnp + "albumArtURI",
                        $"{baseUrl}/thumbnail/{thumbnailKey}.jpg"));
                }

                root.Add(item);
            }
        }

        return new XDocument(root).ToString(SaveOptions.DisableFormatting);
    }

    public static string SoapResponse(string serviceType, string actionName, params XElement[] values)
    {
        XNamespace s = "http://schemas.xmlsoap.org/soap/envelope/";
        XNamespace u = serviceType;
        var document = new XDocument(
            new XElement(s + "Envelope",
                new XAttribute(XNamespace.Xmlns + "s", s.NamespaceName),
                new XAttribute(s + "encodingStyle", "http://schemas.xmlsoap.org/soap/encoding/"),
                new XElement(s + "Body",
                    new XElement(u + $"{actionName}Response",
                        new XAttribute(XNamespace.Xmlns + "u", u.NamespaceName),
                        values))));

        return document.ToString(SaveOptions.DisableFormatting);
    }

    public static string SoapFault(int code, string description)
    {
        XNamespace s = "http://schemas.xmlsoap.org/soap/envelope/";
        var document = new XDocument(
            new XElement(s + "Envelope",
                new XAttribute(XNamespace.Xmlns + "s", s.NamespaceName),
                new XAttribute(s + "encodingStyle", "http://schemas.xmlsoap.org/soap/encoding/"),
                new XElement(s + "Body",
                    new XElement(s + "Fault",
                        new XElement("faultcode", "s:Client"),
                        new XElement("faultstring", "UPnPError"),
                        new XElement("detail",
                            new XElement("UPnPError",
                                new XAttribute(XNamespace.Xmlns + "u", "urn:schemas-upnp-org:control-1-0"),
                                new XElement("errorCode", code),
                                new XElement("errorDescription", description)))))));

        return document.ToString(SaveOptions.DisableFormatting);
    }

    public static string ContentDirectoryServiceType => ContentDirectoryService;

    public static string ConnectionManagerServiceType => ConnectionManagerService;
}
