"use client";

import { FormEvent, useState } from "react";
import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { lookupTransmitterByPostcode } from "@/lib/api";
import { PostcodeLookupResult } from "@/lib/types";

export default function PostcodeLookupPage() {
  const [postcode, setPostcode] = useState("PE7 3GD");
  const [result, setResult] = useState<PostcodeLookupResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setResult(null);
    setLoading(true);
    try {
      const data = await lookupTransmitterByPostcode(postcode.trim());
      setResult(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Lookup failed");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <p className="text-sm uppercase tracking-[0.2em] text-muted-foreground">Transmitters</p>
        <h1 className="text-2xl font-semibold">Find by postcode</h1>
        <p className="text-sm text-muted-foreground">
          Uses Nominatim to geocode UK postcodes and returns the nearest DTT site. Try{" "}
          <button
            type="button"
            className="underline"
            onClick={() => setPostcode("PE7 3GD")}
          >
            PE7 3GD
          </button>{" "}
          for Sandy Heath.
        </p>
        <Button asChild variant="outline" size="sm">
          <Link href="/transmitters">View all transmitters</Link>
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Lookup Transmitter</CardTitle>
        </CardHeader>
        <CardContent>
          <form className="flex flex-col gap-3 md:flex-row" onSubmit={handleSubmit}>
            <Input
              value={postcode}
              onChange={(event) => setPostcode(event.target.value.toUpperCase())}
              placeholder="Enter postcode e.g. PE7 3GD"
              className="md:flex-1"
            />
            <Button type="submit" disabled={loading}>
              {loading ? "Looking up..." : "Lookup Transmitter"}
            </Button>
          </form>
          {error && <p className="mt-3 text-sm text-destructive">{error}</p>}
        </CardContent>
      </Card>

      {result && (
        <Card>
          <CardHeader className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
            <div>
              <CardTitle>{result.transmitter}</CardTitle>
              <p className="text-sm text-muted-foreground">{result.postcode}</p>
              <p className="text-xs text-muted-foreground">
                Lat {result.lat.toFixed(5)} · Lon {result.lon.toFixed(5)}
              </p>
            </div>
            <Badge variant="default">{result.distanceKm.toFixed(1)} km</Badge>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              Closest transmitter to <span className="font-medium">{result.postcode}</span> is{" "}
              <span className="font-medium">{result.transmitter}</span>, approximately{" "}
              {result.distanceKm.toFixed(1)} km away.
            </p>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
