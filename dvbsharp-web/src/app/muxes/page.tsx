export const dynamic = "force-dynamic";

import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { MuxScanForm } from "@/components/mux-scan-form";
import { getMuxes } from "@/lib/api";
import { formatDate } from "@/lib/format";

export default async function MuxesPage() {
  const muxes = await getMuxes().catch(() => []);

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <p className="text-sm uppercase tracking-[0.2em] text-muted-foreground">
          Muxes
        </p>
        <h1 className="text-2xl font-semibold">Transport Streams</h1>
      </div>

      <div className="grid gap-4 md:grid-cols-[1.2fr,0.8fr]">
        <Card>
          <CardHeader>
            <CardTitle>Known Muxes</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Frequency</TableHead>
                  <TableHead>Bandwidth</TableHead>
                  <TableHead>Services</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Updated</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {muxes.map((mux) => (
                  <TableRow key={mux.id}>
                    <TableCell className="font-medium">{mux.frequency} Hz</TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {mux.bandwidth}
                    </TableCell>
                    <TableCell className="text-sm">{mux.services.length}</TableCell>
                    <TableCell>
                      <Badge variant={mux.state === "Locked" ? "default" : "secondary"}>
                        {mux.state}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-xs text-muted-foreground">
                      {mux.lastUpdated ? formatDate(mux.lastUpdated) : "—"}
                    </TableCell>
                  </TableRow>
                ))}
                {muxes.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={5} className="text-center text-sm text-muted-foreground">
                      No muxes stored yet.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </CardContent>
        </Card>

        <MuxScanForm />
      </div>
    </div>
  );
}
