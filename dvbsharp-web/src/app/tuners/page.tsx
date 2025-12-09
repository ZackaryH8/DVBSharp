export const dynamic = "force-dynamic";

import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { getTuners } from "@/lib/api";
import { formatBitrate, formatCapabilities, formatDate } from "@/lib/format";

export default async function TunersPage() {
  const tuners = await getTuners().catch(() => []);

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <p className="text-sm uppercase tracking-[0.2em] text-muted-foreground">
          Tuners
        </p>
        <h1 className="text-2xl font-semibold">Registered Tuners</h1>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Inventory</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Capabilities</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Frequency</TableHead>
                <TableHead>Bitrate</TableHead>
                <TableHead>Updated</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {tuners.map((tuner) => (
                <TableRow key={tuner.id}>
                  <TableCell className="font-medium">
                    <div>{tuner.name}</div>
                    <p className="text-xs text-muted-foreground">{tuner.type}</p>
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {formatCapabilities(tuner.capabilities)}
                  </TableCell>
                  <TableCell>
                    <Badge variant={tuner.status?.isStreaming ? "default" : "secondary"}>
                      {tuner.status?.isStreaming ? "Streaming" : "Idle"}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-sm">
                    {tuner.status?.frequency ? `${tuner.status.frequency} Hz` : "—"}
                  </TableCell>
                  <TableCell className="text-sm">
                    {tuner.status ? formatBitrate(tuner.status.bitrateBps) : "—"}
                  </TableCell>
                  <TableCell className="text-xs text-muted-foreground">
                    {tuner.status?.lastUpdated ? formatDate(tuner.status.lastUpdated) : "—"}
                  </TableCell>
                </TableRow>
              ))}
              {tuners.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} className="text-center text-sm text-muted-foreground">
                    No tuners found.
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
