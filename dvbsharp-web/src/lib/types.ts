export type ApiResponse<T> = {
  success: boolean;
  data: T;
  error?: string;
};

export type TunerStatus = {
  tunerId: string;
  frequency: number;
  isStreaming: boolean;
  packetCount: number;
  bitrateBps: number;
  lastUpdated: string;
};

export type TunerInfo = {
  id: string;
  name: string;
  type: string;
  description?: string;
  capabilities: string[];
};

// /api/tuners list items: flattened info + optional status
export type TunerListItem = TunerInfo & {
  status?: TunerStatus;
};

// /api/tuners/{id} details: info + status
export type TunerWithStatus = {
  info: TunerInfo;
  status: TunerStatus;
};

export type TunerAssignment = {
  tunerId: string;
  frequency: number;
  label?: string;
};

export type StreamInfo = {
  type: string;
  pid: number;
  codec: string;
};

export type Service = {
  serviceId: number;
  name: string;
  pmtPid: number;
  audioPids: number[];
  videoPids: number[];
  streams?: StreamInfo[];
  logicalChannelNumber?: number;
  callSign?: string;
  category?: string;
};

export type MuxState = "Unknown" | "Scanning" | "Locked" | "Error";

export type Mux = {
  id: string;
  frequency: number;
  bandwidth: number;
  state: MuxState;
  lastUpdated: string;
  services: Service[];
};

export type Channel = {
  muxId: string;
  frequency: number;
  serviceId: number;
  name: string;
  pmtPid: number;
  audioPids: number[];
  videoPids: number[];
  streams?: StreamInfo[];
  logicalChannelNumber?: number;
  callSign?: string;
  category?: string;
};

export type ChannelCategorySummary = {
  category: string;
  count: number;
};

export type ChannelSummary = {
  totalChannels: number;
  muxCount: number;
  channelsWithLcn: number;
  logicalChannelCoverage: number;
  lastUpdated?: string;
  categories: ChannelCategorySummary[];
};

export type HdHomeRunEndpointInfo = {
  discover: string;
  status: string;
  lineup: string;
  lineupPost: string;
  streamAny: string;
};

export type HdHomeRunLineupPreview = {
  guideNumber: string;
  guideName: string;
  url: string;
  callSign?: string;
  category?: string;
};

export type HdHomeRunInfo = {
  friendlyName: string;
  deviceId: string;
  deviceAuth: string;
  manufacturer: string;
  modelNumber: string;
  firmwareName: string;
  firmwareVersion: string;
  sourceType: string;
  tunerCount: number;
  channelCount: number;
  baseUrl: string;
  endpoints: HdHomeRunEndpointInfo;
  lineupPreview: HdHomeRunLineupPreview[];
};

export type TransmitterMux = {
  name: string;
  uhfChannel?: number;
  frequencyMHz?: number;
  erpKW?: number;
};

export type Transmitter = {
  siteName: string;
  postcode?: string;
  region?: string;
  latitude: number;
  longitude: number;
  isRelay: boolean;
  muxes: TransmitterMux[];
};

export type TransmitterPage = {
  items: Transmitter[];
  total: number;
  skip: number;
  take: number;
};

export type NearestTransmitter = Transmitter & {
  distanceKm: number;
};

export type PostcodeLookupResult = {
  postcode: string;
  lat: number;
  lon: number;
  transmitter: string;
  distanceKm: number;
};
