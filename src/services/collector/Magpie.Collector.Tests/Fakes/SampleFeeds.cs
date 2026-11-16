namespace Magpie.Collector.Tests.Fakes;

/// <summary>Small hand-written feeds in the two formats, plus some rubbish.</summary>
public static class SampleFeeds
{
    public const string Rss = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0" xmlns:dc="http://purl.org/dc/elements/1.1/">
          <channel>
            <title>Example Blog</title>
            <link>https://blog.example.com/</link>
            <item>
              <title>First &amp; foremost</title>
              <link>https://blog.example.com/first</link>
              <guid>post-1</guid>
              <pubDate>Tue, 10 Nov 2026 08:30:00 GMT</pubDate>
              <dc:creator>Asha</dc:creator>
              <description><![CDATA[<p>Hello <b>world</b>.</p>   <p>Second paragraph.</p>]]></description>
            </item>
            <item>
              <title>Second post</title>
              <link>https://blog.example.com/second</link>
              <guid>post-2</guid>
            </item>
            <item>
              <title>An item with no link is skipped</title>
            </item>
          </channel>
        </rss>
        """;

    public const string Atom = """
        <?xml version="1.0" encoding="utf-8"?>
        <feed xmlns="http://www.w3.org/2005/Atom">
          <title>Example Atom Feed</title>
          <entry>
            <id>tag:example.com,2026:entry-1</id>
            <title>An Atom entry</title>
            <link rel="self" href="https://example.com/api/entry-1"/>
            <link rel="alternate" href="https://example.com/entry-1"/>
            <published>2026-11-12T09:00:00Z</published>
            <author><name>Ben</name></author>
            <summary>Short and plain.</summary>
          </entry>
        </feed>
        """;

    public const string NotXml = "<html><body><h1>503 Service Temporarily Unavailable</h1><br>nginx";

    public const string XmlButNotAFeed = "<?xml version=\"1.0\"?><sitemap><url>https://example.com/</url></sitemap>";

    /// <summary>A document that tries to use the dangerous XML features we switched off.</summary>
    public const string WithDtd = """
        <?xml version="1.0"?>
        <!DOCTYPE rss [ <!ENTITY big "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"> ]>
        <rss version="2.0"><channel><item><title>&big;</title><link>https://example.com/x</link></item></channel></rss>
        """;
}
