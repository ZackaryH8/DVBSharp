export const dynamic = "force-dynamic";

import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { getTransmitters } from "@/lib/api";

type TransmittersPageProps = {
  searchParams?: Promise<{ page?: string }>;
};

export default async function TransmittersPage({ searchParams }: TransmittersPageProps) {
  const params = (await searchParams) ?? {};
  const page = Math.max(1, Number(params.page ?? "1"));
  const take = 25;
  const skip = (page - 1) * take;
  const data = await getTransmitters(skip, take).catch(() => null);
  const transmitters = data?.items ?? [];
  const total = data?.total ?? 0;
  const maxPage = Math.max(1, Math.ceil(total / take));

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
        <div>
          <p className="text-sm uppercase tracking-[0.2em] text-muted-foreground">Transmitters</p>
          <h1 className="text-2xl font-semibold">UK DTT Sites</h1>
          <p className="text-sm text-muted-foreground">Parsed from the public Ofcom transmitter CSV.</p>
        </div>
        <Button asChild variant="outline">
          <Link href="/transmitters/postcode">Lookup by postcode</Link>
        </Button>
      </div>

      <div className="grid gap-4">
        {transmitters.map((tx) => (
          <Card key={`${tx.siteName}-${tx.latitude}-${tx.longitude}`}>
            <CardHeader className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
              <div>
                <CardTitle>{tx.siteName}</CardTitle>
                <p className="text-sm text-muted-foreground">
                  {tx.region ?? "Unknown region"} · {tx.postcode ?? "Unknown postcode"}
                </p>
                <p className="text-xs text-muted-foreground">
                  Lat {tx.latitude.toFixed(4)} · Lon {tx.longitude.toFixed(4)}
                </p>
              </div>
              <Badge variant={tx.isRelay ? "secondary" : "default"}>{tx.isRelay ? "Relay" : "Main"}</Badge>
            </CardHeader>
            <CardContent>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Mux</TableHead>
                    <TableHead>UHF Ch</TableHead>
                    <TableHead>Freq (MHz)</TableHead>
                    <TableHead>ERP (kW)</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {tx.muxes.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={4} className="text-center text-sm text-muted-foreground">
                        No mux data available.
                      </TableCell>
                    </TableRow>
                  )}
                  {tx.muxes.map((mux) => (
                    <TableRow key={`${tx.siteName}-${mux.name}`}>
                      <TableCell className="font-medium">{mux.name}</TableCell>
                      <TableCell>{mux.uhfChannel ?? "—"}</TableCell>
                      <TableCell>{mux.frequencyMHz ? mux.frequencyMHz.toFixed(1) : "—"}</TableCell>
                      <TableCell>{mux.erpKW ? mux.erpKW.toFixed(1) : "—"}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        ))}
        {transmitters.length === 0 && (
          <Card>
            <CardHeader>
              <CardTitle>No transmitters available</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-muted-foreground">
                The backend did not return any transmitters. Ensure the CSV dataset exists under <code>data/</code>.
              </p>
            </CardContent>
          </Card>
        )}
      </div>

      {total > take && (
        <div className="flex items-center justify-between rounded-md border px-3 py-2 text-sm">
          <p>
            Showing {(skip + 1).toLocaleString()}-
            {Math.min(skip + take, total).toLocaleString()} of {total.toLocaleString()}
          </p>
          <div className="flex gap-2">
            <Button asChild variant="outline" size="sm" disabled={page <= 1}>
              <Link href={`/transmitters?page=${page - 1}`}>Previous</Link>
            </Button>
            <Button asChild variant="outline" size="sm" disabled={page >= maxPage}>
              <Link href={`/transmitters?page=${page + 1}`}>Next</Link>
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
