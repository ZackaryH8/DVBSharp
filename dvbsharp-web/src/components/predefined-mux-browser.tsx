"use client";

import { useMemo, useState } from "react";
import { CopyButton } from "@/components/copy-button";
import { Badge } from "@/components/ui/badge";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { PredefinedMuxLocation } from "@/lib/types";
import { formatDate } from "@/lib/format";

type PredefinedMuxBrowserProps = {
  locations: PredefinedMuxLocation[];
};

export function PredefinedMuxBrowser({ locations }: PredefinedMuxBrowserProps) {
  const [selectedId, setSelectedId] = useState(locations[0]?.id ?? "");

  const selected = useMemo(
    () => locations.find((loc) => loc.id === selectedId) ?? locations[0],
    [locations, selectedId],
  );

  if (locations.length === 0) {
    return <p className="text-sm text-muted-foreground">Predefined mux tables are not available yet.</p>;
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-1">
        <label htmlFor="predefined-mux-select" className="text-sm font-medium">
          Select transmitter
        </label>
        <select
          id="predefined-mux-select"
          className="rounded-md border bg-background px-3 py-2 text-sm"
          value={selected?.id ?? ""}
          onChange={(event) => setSelectedId(event.target.value)}
        >
          {locations.map((location) => (
            <option key={location.id} value={location.id}>
              {location.name}
              {location.country ? ` · ${location.country}` : ""}
            </option>
          ))}
        </select>
        {selected && (
          <p className="text-xs text-muted-foreground">
            {selected.provider ?? selected.name}
            {selected.sourceDate ? ` · ${formatDate(selected.sourceDate)}` : ""}
            {" · "}
            {selected.muxes.length} muxes
          </p>
        )}
      </div>

      {selected ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Frequency</TableHead>
              <TableHead>System</TableHead>
              <TableHead className="hidden lg:table-cell">Modulation</TableHead>
              <TableHead className="hidden lg:table-cell">Mode</TableHead>
              <TableHead className="w-[80px]">Copy</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {selected.muxes.map((mux) => (
              <TableRow key={`${selected.id}-${mux.name}`}>
                <TableCell className="font-medium">{mux.name}</TableCell>
                <TableCell className="text-sm">
                  {mux.frequency.toLocaleString()} Hz
                  <span className="ml-2 text-xs text-muted-foreground">
                    {(mux.bandwidthHz / 1_000_000).toFixed(1)} MHz
                  </span>
                </TableCell>
                <TableCell>
                  <Badge variant="secondary">{mux.deliverySystem}</Badge>
                </TableCell>
                <TableCell className="hidden text-xs text-muted-foreground lg:table-cell">
                  {mux.modulation ?? "—"}
                </TableCell>
                <TableCell className="hidden text-xs text-muted-foreground lg:table-cell">
                  {mux.transmissionMode ?? "—"}
                </TableCell>
                <TableCell>
                  <CopyButton value={String(mux.frequency)} size="sm">
                    Copy
                  </CopyButton>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <p className="text-sm text-muted-foreground">Choose a transmitter to view its muxes.</p>
      )}
    </div>
  );
}
