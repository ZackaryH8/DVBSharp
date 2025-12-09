"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { assignTuner, unassignTuner } from "@/lib/api";
import { Mux, TunerAssignment, TunerListItem } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

type PinTunerFormProps = {
  tuners: TunerListItem[];
  muxes: Mux[];
  assignments: TunerAssignment[];
};

export function PinTunerForm({ tuners, muxes, assignments }: PinTunerFormProps) {
  const [tunerId, setTunerId] = useState(tuners[0]?.id ?? "");
  const [frequency, setFrequency] = useState<string>(muxes[0]?.frequency.toString() ?? "");
  const [label, setLabel] = useState("Cambridge");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const router = useRouter();

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!tunerId) {
      setError("Select a tuner");
      return;
    }

    const freq = Number(frequency);
    if (!freq || Number.isNaN(freq)) {
      setError("Enter a frequency");
      return;
    }

    setBusy(true);
    setError(null);
    try {
      await assignTuner({ tunerId, frequency: freq, label });
      router.refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to pin tuner");
    } finally {
      setBusy(false);
    }
  };

  const release = async (id: string) => {
    setBusy(true);
    try {
      await unassignTuner(id);
      router.refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to unassign tuner");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6">
      <form onSubmit={submit} className="grid gap-4 rounded-md border p-4">
        <div className="grid gap-1">
          <label htmlFor="tuner" className="text-sm font-medium">
            Select tuner
          </label>
          <select
            id="tuner"
            className="rounded-md border bg-background px-3 py-2 text-sm"
            value={tunerId}
            onChange={(e) => setTunerId(e.target.value)}
          >
            {tuners.map((tuner) => (
              <option key={tuner.id} value={tuner.id}>
                {tuner.name}
              </option>
            ))}
          </select>
        </div>
        <div className="grid gap-1">
          <label htmlFor="frequency" className="text-sm font-medium">
            Frequency (Hz)
          </label>
          <select
            id="frequency"
            className="rounded-md border bg-background px-3 py-2 text-sm"
            value={frequency}
            onChange={(e) => setFrequency(e.target.value)}
          >
            {muxes.map((mux) => (
              <option key={mux.id} value={mux.frequency}>
                {mux.frequency.toLocaleString()} Hz — {mux.services.length} services
              </option>
            ))}
          </select>
        </div>
        <div className="grid gap-1">
          <label htmlFor="label" className="text-sm font-medium">
            Label (optional)
          </label>
          <Input id="label" value={label} onChange={(e) => setLabel(e.target.value)} placeholder="e.g. Sandy Heath" />
        </div>
        {error && <p className="text-sm text-destructive">{error}</p>}
        <Button type="submit" disabled={busy}>
          {busy ? "Saving..." : "Pin tuner"}
        </Button>
      </form>

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Tuner</TableHead>
              <TableHead>Frequency</TableHead>
              <TableHead>Label</TableHead>
              <TableHead></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {assignments.length === 0 && (
              <TableRow>
                <TableCell colSpan={4} className="text-center text-sm text-muted-foreground">
                  No pins yet. Assign a tuner above.
                </TableCell>
              </TableRow>
            )}
            {assignments.map((assignment) => (
              <TableRow key={assignment.tunerId}>
                <TableCell className="font-medium">{assignment.tunerId}</TableCell>
                <TableCell>{assignment.frequency.toLocaleString()} Hz</TableCell>
                <TableCell>{assignment.label ?? "—"}</TableCell>
                <TableCell className="text-right">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => release(assignment.tunerId)}
                    disabled={busy}
                  >
                    Release
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
