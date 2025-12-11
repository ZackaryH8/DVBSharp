export const dynamic = "force-dynamic";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Separator } from "@/components/ui/separator";
import { CopyButton } from "@/components/copy-button";
import { HdHomeRunLimitForm } from "@/components/hdhomerun-limit-form";
import { getChannelSummary, getChannels, getHdHomeRunInfo } from "@/lib/api";
import { formatDate } from "@/lib/format";
import { ChannelSummary, HdHomeRunInfo } from "@/lib/types";

export default async function ChannelsPage() {
  const [channels, summary, hdhr] = await Promise.all([
    getChannels().catch(() => []),
    getChannelSummary().catch(() => null),
    getHdHomeRunInfo().catch(() => null),
  ]);

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <p className="text-sm uppercase tracking-[0.2em] text-muted-foreground">
          Channels
        </p>
        <h1 className="text-2xl font-semibold">Discovered Services</h1>
      </div>

      {summary && <ChannelSummaryPanel summary={summary} />}

      {hdhr && <HdHomeRunPanel info={hdhr} />}

      <Card>
        <CardHeader>
          <CardTitle>Services</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>LCN</TableHead>
                <TableHead>Name</TableHead>
                <TableHead>Service ID</TableHead>
                <TableHead>Category</TableHead>
                <TableHead>Mux Frequency</TableHead>
                <TableHead>Audio PIDs</TableHead>
                <TableHead>Video PIDs</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {channels.map((ch) => (
                <TableRow key={`${ch.muxId}-${ch.serviceId}`}>
                  <TableCell>
                    {typeof ch.logicalChannelNumber === "number" ? (
                      <Badge variant="outline">{ch.logicalChannelNumber}</Badge>
                    ) : (
                      <span className="text-xs text-muted-foreground">—</span>
                    )}
                  </TableCell>
                  <TableCell className="font-medium">
                    <div className="flex items-center gap-2">
                      <span>{ch.name}</span>
                      {ch.callSign && (
                        <Badge variant="secondary" className="text-[0.65rem]">
                          {ch.callSign}
                        </Badge>
                      )}
                    </div>
                  </TableCell>
                  <TableCell>{ch.serviceId}</TableCell>
                  <TableCell>
                    {ch.category ? (
                      <Badge variant="outline">{ch.category}</Badge>
                    ) : (
                      <span className="text-xs text-muted-foreground">—</span>
                    )}
                  </TableCell>
                  <TableCell>{ch.frequency} Hz</TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {ch.audioPids.join(", ")}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {ch.videoPids.join(", ")}
                  </TableCell>
                </TableRow>
              ))}
              {channels.length === 0 && (
                <TableRow>
                  <TableCell colSpan={7} className="text-center text-sm text-muted-foreground">
                    No channels discovered yet. Scan a mux first.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}

function ChannelSummaryPanel({ summary }: { summary: ChannelSummary }) {
  const coveragePercent = (summary.logicalChannelCoverage * 100).toFixed(1);

  return (
    <div className="grid gap-4 lg:grid-cols-3">
      <Card>
        <CardHeader>
          <CardTitle>Lineup Overview</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3 text-sm">
          <div className="flex items-center justify-between">
            <span className="text-muted-foreground">Total channels</span>
            <span className="text-base font-semibold">{summary.totalChannels}</span>
          </div>
          <div className="flex items-center justify-between">
            <span className="text-muted-foreground">Muxes tracked</span>
            <span className="text-base font-semibold">{summary.muxCount}</span>
          </div>
          <div className="flex items-center justify-between">
            <span className="text-muted-foreground">Channels with LCN</span>
            <span className="text-base font-semibold">
              {summary.channelsWithLcn} ({coveragePercent}%)
            </span>
          </div>
          <Separator />
          <div className="text-xs text-muted-foreground">
            Last updated: {summary.lastUpdated ? formatDate(summary.lastUpdated) : "—"}
          </div>
        </CardContent>
      </Card>

      <Card className="lg:col-span-2">
        <CardHeader>
          <CardTitle>Categories</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-wrap gap-2">
          {summary.categories.length === 0 && (
            <p className="text-sm text-muted-foreground">No category metadata yet.</p>
          )}
          {summary.categories.map((cat) => (
            <Badge key={cat.category} variant="outline" className="text-sm">
              {cat.category} · {cat.count}
            </Badge>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}

function HdHomeRunPanel({ info }: { info: HdHomeRunInfo }) {
  const endpointEntries: Array<{ label: string; value: string }> = [
    { label: "Discover", value: info.endpoints.discover },
    { label: "Lineup status", value: info.endpoints.status },
    { label: "Lineup", value: info.endpoints.lineup },
    { label: "Lineup POST", value: info.endpoints.lineupPost },
    { label: "Stream (automatic)", value: info.endpoints.streamAny },
  ];

  return (
    <Card>
      <CardHeader>
        <CardTitle>HDHomeRun Integration</CardTitle>
        <p className="text-sm text-muted-foreground">
          Present DVBSharp as a virtual HDHomeRun device for clients like Plex, Jellyfin, or Channels.
        </p>
      </CardHeader>
      <CardContent className="space-y-6">
        <div className="grid gap-4 md:grid-cols-4">
          <Stat label="Device" value={info.friendlyName} />
          <Stat label="Advertised tuners" value={info.tunerCount.toString()} />
          <Stat label="Physical tuners" value={(info.physicalTuners ?? info.tunerCount).toString()} />
          <Stat label="Channels in lineup" value={info.channelCount.toString()} />
        </div>
        <HdHomeRunLimitForm initialLimit={info.tunerLimit ?? undefined} />

        <div className="space-y-3">
          <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
            <div>
              <p className="text-sm font-medium">Base URL</p>
              <p className="text-sm text-muted-foreground">Point clients here to auto-discover DVBSharp.</p>
            </div>
            <div className="flex items-center gap-2">
              <code className="rounded-md bg-muted px-2 py-1 text-sm">{info.baseUrl}</code>
              <CopyButton value={info.baseUrl} variant="outline" size="sm">
                Copy URL
              </CopyButton>
            </div>
          </div>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Endpoint</TableHead>
                <TableHead>URL</TableHead>
                <TableHead className="w-[80px]">Copy</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {endpointEntries.map((row) => (
                <TableRow key={row.label}>
                  <TableCell className="font-medium">{row.label}</TableCell>
                  <TableCell className="text-sm">
                    <code className="rounded bg-muted px-2 py-1">{row.value}</code>
                  </TableCell>
                  <TableCell>
                    <CopyButton value={row.value} size="sm" variant="outline">
                      Copy
                    </CopyButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>

      </CardContent>
    </Card>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border p-3">
      <p className="text-xs uppercase tracking-wide text-muted-foreground">{label}</p>
      <p className="text-2xl font-semibold">{value}</p>
    </div>
  );
}
