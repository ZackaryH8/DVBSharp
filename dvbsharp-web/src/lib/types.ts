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

export type PredefinedMux = {
  name: string;
  frequency: number;
  bandwidthHz: number;
  deliverySystem: string;
  modulation?: string;
  transmissionMode?: string;
  guardInterval?: string;
  codeRateHp?: string;
  codeRateLp?: string;
  streamId?: string;
};

export type PredefinedMuxLocation = {
  id: string;
  name: string;
  country?: string;
  provider?: string;
  sourceDate?: string;
  muxes: PredefinedMux[];
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
  physicalTuners: number;
  tunerLimit?: number | null;
  channelCount: number;
  baseUrl: string;
  endpoints: HdHomeRunEndpointInfo;
};

export type HdHomeRunSettings = {
  tunerLimit?: number | null;
};

export type ActiveStream = {
  id: string;
  tunerId: string;
  frequency?: number;
  label?: string;
  client?: string;
  startedAt: string;
};
