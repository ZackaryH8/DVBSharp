export const dynamic = "force-dynamic";

import Link from "next/link";
import { getActiveStreams, getChannels, getMuxes, getTuners } from "@/lib/api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { ActiveStream, Channel, Mux, TunerListItem } from "@/lib/types";
import { formatCapabilities } from "@/lib/format";

export default async function Home() {
  let tuners: TunerListItem[] = [];
  let muxes: Mux[] = [];
  let channels: Channel[] = [];
  let streams: ActiveStream[] = [];

  try {
    [tuners, muxes, channels, streams] = await Promise.all([
      getTuners(),
      getMuxes(),
      getChannels(),
      getActiveStreams(),
    ]);
  } catch (err) {
    return (
      <div className="space-y-6">
        <Header />
        <Card>
          <CardHeader>
            <CardTitle>Backend not reachable</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              {err instanceof Error ? err.message : "Unknown error"}
            </p>
          </CardContent>
        </Card>
      </div>
    );
  }

  const streaming = tuners.filter((t) => t.status?.isStreaming).length;

  return (
    <div className="space-y-6">
      <Header />

      <div className="grid gap-4 md:grid-cols-4">
        <StatCard label="Tuners" value={tuners.length} href="/tuners" />
        <StatCard label="Streaming" value={streaming} href="/tuners" />
        <StatCard label="Muxes" value={muxes.length} href="/muxes" />
        <StatCard label="Channels" value={channels.length} href="/channels" />
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Active Tuners</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {tuners.length === 0 && (
              <p className="text-sm text-muted-foreground">No tuners available.</p>
            )}
            {tuners.map((tuner) => (
              <div
                key={tuner.id}
                className="flex items-center justify-between rounded-md border p-3"
              >
                <div>
                  <p className="font-medium">{tuner.name}</p>
                  <p className="text-xs text-muted-foreground">
                    Capabilities ({formatCapabilities(tuner.capabilities)})
                  </p>
                </div>
                <Badge variant={tuner.status?.isStreaming ? "default" : "secondary"}>
                  {tuner.status?.isStreaming ? "Streaming" : "Idle"}
                </Badge>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between">
            <CardTitle>Recent Muxes</CardTitle>
            <Button asChild variant="ghost" size="sm">
              <Link href="/muxes">View all</Link>
            </Button>
          </CardHeader>
          <CardContent className="space-y-2">
            {muxes.slice(0, 5).map((mux) => (
              <div key={mux.id} className="rounded-md border p-3">
                <div className="flex items-center justify-between">
                  <p className="font-medium">{mux.frequency} Hz</p>
                  <Badge variant="secondary">{mux.state}</Badge>
                </div>
                <p className="text-xs text-muted-foreground">
                  Services: {mux.services.length}
                </p>
              </div>
            ))}
            {muxes.length === 0 && (
              <p className="text-sm text-muted-foreground">No muxes stored yet.</p>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>Live Streams</CardTitle>
          <Badge variant="outline">{streams.length}</Badge>
        </CardHeader>
        <CardContent>
          {streams.length === 0 ? (
            <p className="text-sm text-muted-foreground">No active streams.</p>
          ) : (
            <div className="space-y-3">
              {streams.map((stream) => (
                <div key={stream.id} className="rounded-md border p-3">
                  <div className="flex items-center justify-between text-sm">
                    <div>
                      <p className="font-medium">{stream.tunerId}</p>
                      <p className="text-xs text-muted-foreground">
                        {stream.label ?? "Unlabelled"}
                      </p>
                    </div>
                    <Badge variant="secondary">
                      {stream.frequency ? `${stream.frequency.toLocaleString()} Hz` : "Override"}
                    </Badge>
                  </div>
                  <div className="mt-2 flex items-center justify-between text-xs text-muted-foreground">
                    <span>Client: {stream.client ?? "Unknown"}</span>
                    <span>Started {formatSince(stream.startedAt)}</span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>Top Channels</CardTitle>
          <Button asChild variant="ghost" size="sm">
            <Link href="/channels">View all</Link>
          </Button>
        </CardHeader>
        <CardContent className="grid gap-2 md:grid-cols-2">
          {channels.slice(0, 6).map((ch) => (
            <div key={`${ch.muxId}-${ch.serviceId}`} className="rounded-md border p-3">
              <div className="flex items-center justify-between">
                <div>
                  <p className="font-medium">{ch.name}</p>
                  <p className="text-xs text-muted-foreground">SID {ch.serviceId}</p>
                </div>
                <Badge variant="outline">{ch.frequency} Hz</Badge>
              </div>
              <p className="text-xs text-muted-foreground">
                Audio: {ch.audioPids.join(", ")} · Video: {ch.videoPids.join(", ")}
              </p>
            </div>
          ))}
          {channels.length === 0 && (
            <p className="text-sm text-muted-foreground">No channels discovered.</p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function formatSince(iso: string) {
  const started = new Date(iso).getTime();
  const diffMs = Date.now() - started;
  const diffMin = Math.floor(diffMs / 60000);
  if (diffMin <= 0) return "just now";
  if (diffMin < 60) return `${diffMin}m ago`;
  const hours = (diffMin / 60).toFixed(1);
  return `${hours}h ago`;
}

function Header() {
  return (
    <div className="space-y-2">
      <p className="text-sm uppercase tracking-[0.2em] text-muted-foreground">
        DVBSharp
      </p>
      <h1 className="text-3xl font-semibold">Monitoring Console</h1>
      <p className="text-sm text-muted-foreground">
        Track tuners, mux scans, and discovered channels.
      </p>
      <div className="flex gap-2">
        <Button asChild size="sm">
          <Link href="/muxes">Scan mux</Link>
        </Button>
        <Button asChild size="sm" variant="outline">
          <Link href="/tuners">View tuners</Link>
        </Button>
      </div>
    </div>
  );
}

function StatCard({
  label,
  value,
  href,
}: {
  label: string;
  value: number | string;
  href: string;
}) {
  return (
    <Link href={href} className="group">
      <Card className="transition hover:-translate-y-0.5 hover:shadow-md">
        <CardHeader className="pb-2">
          <p className="text-xs uppercase tracking-wide text-muted-foreground">
            {label}
          </p>
        </CardHeader>
        <CardContent className="flex items-center justify-between">
          <p className="text-3xl font-semibold">{value}</p>
          <Badge variant="outline" className="text-xs">
            View
          </Badge>
        </CardContent>
      </Card>
    </Link>
  );
}
