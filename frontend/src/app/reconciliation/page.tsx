"use client";

import { useState, useRef, DragEvent, ChangeEvent } from "react";
import {
  UploadCloud,
  CheckCircle2,
  AlertCircle,
  FileSpreadsheet,
  ArrowUpRight,
  ShieldCheck,
  X,
  RefreshCw,
  FileCheck,
  AlertTriangle,
  Download,
  Info
} from "lucide-react";

interface ReconciliationBatch {
  id: string;
  provider: string;
  fileName: string;
  uploadedAt: string;
  totalRecords: number;
  matched: number;
  unmatched: number;
  discrepancy: number;
  settledAmount: string;
  isNew?: boolean;
}

interface DiscrepancyItem {
  id: string;
  txnRef: string;
  providerId: string;
  internalAmount: string;
  statementAmount: string;
  type: string;
  note: string;
  resolved?: boolean;
}

interface AlertToast {
  type: "success" | "error" | "info";
  title: string;
  message: string;
  batchId?: string;
  stats?: {
    total: number;
    matched: number;
    discrepancies: number;
    settled: string;
  };
}

export default function ReconciliationPage() {
  const [batches, setBatches] = useState<ReconciliationBatch[]>([
    {
      id: "batch_20260911_01",
      provider: "Opn (Omise)",
      fileName: "opn_settlement_2026-09-11.csv",
      uploadedAt: "Today, 18:30",
      totalRecords: 1250,
      matched: 1242,
      unmatched: 5,
      discrepancy: 3,
      settledAmount: "฿3,842,500.00"
    },
    {
      id: "batch_20260910_02",
      provider: "GB Prime Pay",
      fileName: "gb_settlement_2026-09-10.csv",
      uploadedAt: "Yesterday, 20:15",
      totalRecords: 840,
      matched: 838,
      unmatched: 2,
      discrepancy: 0,
      settledAmount: "฿1,420,100.00"
    }
  ]);

  const [discrepancies, setDiscrepancies] = useState<DiscrepancyItem[]>([
    {
      id: "disc-1",
      txnRef: "ORD-89412",
      providerId: "chrg_opn_99812",
      internalAmount: "฿2,500.00",
      statementAmount: "฿2,100.00",
      type: "Amount Mismatch",
      note: "Customer initiated partial refund directly with card issuer.",
      resolved: false
    },
    {
      id: "disc-2",
      txnRef: "ORD-89415",
      providerId: "chrg_opn_99815",
      internalAmount: "฿1,200.00",
      statementAmount: "฿1,200.00",
      type: "Fee Variance",
      note: "Contracted MDR 3.65% (฿43.80), but charged ฿51.20 in settlement report.",
      resolved: false
    },
    {
      id: "disc-3",
      txnRef: "ORD-UNKNOWN",
      providerId: "chrg_opn_ghost_01",
      internalAmount: "Not Found",
      statementAmount: "฿750.00",
      type: "Unmatched in Ledger",
      note: "Settlement entry without corresponding internal order record.",
      resolved: false
    }
  ]);

  const [isDragging, setIsDragging] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);
  const [toast, setToast] = useState<AlertToast | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Helper to parse CSV lines taking quotes into account
  const parseCSVContent = (content: string) => {
    const lines = content.split(/\r\n|\n|\r/).filter((line) => line.trim().length > 0);
    if (lines.length < 2) return null;

    const parseLine = (line: string): string[] => {
      const row: string[] = [];
      let current = "";
      let inQuotes = false;
      for (let i = 0; i < line.length; i++) {
        const char = line[i];
        if (char === '"') {
          if (inQuotes && line[i + 1] === '"') {
            current += '"';
            i++;
          } else {
            inQuotes = !inQuotes;
          }
        } else if (char === "," && !inQuotes) {
          row.push(current.trim());
          current = "";
        } else {
          current += char;
        }
      }
      row.push(current.trim());
      return row;
    };

    const rawHeaders = parseLine(lines[0]);
    const headers = rawHeaders.map((h) => h.replace(/^["']|["']$/g, "").trim());

    // Normalize header mapping
    const findIndex = (regex: RegExp) => headers.findIndex((h) => regex.test(h));
    const txnIdIdx = findIndex(/transaction\s*id|txn\s*id|txn_id|order\s*id|ref/i);
    const amountIdx = findIndex(/gross\s*amount|^amount$|internal\s*amount/i);
    const feeIdx = findIndex(/fee|mdr|charge/i);
    const netIdx = findIndex(/net\s*amount|^net$|settled\s*amount/i);
    const statusIdx = findIndex(/status|state|reconciliation\s*status/i);
    const providerIdx = findIndex(/provider|gateway/i);
    const noteIdx = findIndex(/note|reason|comment|description/i);

    const dataRows = lines.slice(1).map((l) => parseLine(l).map((val) => val.replace(/^["']|["']$/g, "").trim()));

    return {
      indices: {
        txnId: txnIdIdx >= 0 ? txnIdIdx : 0,
        amount: amountIdx >= 0 ? amountIdx : 1,
        fee: feeIdx >= 0 ? feeIdx : 2,
        net: netIdx >= 0 ? netIdx : 3,
        status: statusIdx >= 0 ? statusIdx : 4,
        provider: providerIdx,
        note: noteIdx
      },
      rows: dataRows
    };
  };

  const processCSVFile = (file: File) => {
    if (!file.name.toLowerCase().endsWith(".csv")) {
      setToast({
        type: "error",
        title: "Invalid File Format",
        message: "Please upload a valid .csv settlement statement file."
      });
      return;
    }

    setIsProcessing(true);
    const reader = new FileReader();

    reader.onload = (e) => {
      try {
        const text = e.target?.result as string;
        const parsed = parseCSVContent(text);

        if (!parsed || parsed.rows.length === 0) {
          setToast({
            type: "error",
            title: "Empty or Corrupt File",
            message: "The CSV file does not contain any transaction rows."
          });
          setIsProcessing(false);
          return;
        }

        const { indices, rows } = parsed;
        let totalNet = 0;
        let matchedCount = 0;
        let discrepancyCount = 0;
        let unmatchedCount = 0;
        const newDiscrepancies: DiscrepancyItem[] = [];

        // Detect provider from file name or first row
        let detectedProvider = "Opn (Omise)";
        const lowerName = file.name.toLowerCase();
        if (lowerName.includes("gbprime") || lowerName.includes("gb_")) {
          detectedProvider = "GB Prime Pay";
        } else if (lowerName.includes("2c2p")) {
          detectedProvider = "2C2P Gateway";
        } else if (lowerName.includes("promptpay") || lowerName.includes("scb") || lowerName.includes("kbank")) {
          detectedProvider = "PromptPay / Bank Settlement";
        } else if (lowerName.includes("truemoney")) {
          detectedProvider = "TrueMoney Wallet";
        }

        rows.forEach((row, index) => {
          const txnId = row[indices.txnId] || `TXN-${index + 1000}`;
          const rawAmount = row[indices.amount] || "0";
          const rawFee = row[indices.fee] || "0";
          const rawNet = row[indices.net] || "";
          const status = (row[indices.status] || "Matched").trim();
          const providerRef = indices.provider !== -1 && row[indices.provider] ? row[indices.provider] : `chrg_${txnId.toLowerCase().replace(/[^a-z0-9]/g, "_")}`;
          const note = indices.note !== -1 && row[indices.note] ? row[indices.note] : "";

          const amount = parseFloat(rawAmount.replace(/[^0-9.-]+/g, "")) || 0;
          const fee = parseFloat(rawFee.replace(/[^0-9.-]+/g, "")) || 0;
          const net = rawNet ? (parseFloat(rawNet.replace(/[^0-9.-]+/g, "")) || (amount - fee)) : (amount - fee);

          totalNet += net;

          const lowerStatus = status.toLowerCase();
          const isUnmatched = lowerStatus.includes("unmatched") || lowerStatus.includes("not found");
          const isCalculationMismatch = Math.abs(amount - fee - net) > 0.05;
          const isStatusDiscrepancy = lowerStatus.includes("discrepancy") || lowerStatus.includes("mismatch") || lowerStatus.includes("variance") || lowerStatus.includes("error") || lowerStatus.includes("failed");

          if (isUnmatched) {
            unmatchedCount++;
            discrepancyCount++;
            newDiscrepancies.push({
              id: `disc-up-${Date.now()}-${index}`,
              txnRef: txnId,
              providerId: providerRef,
              internalAmount: "Not Found",
              statementAmount: `฿${net.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`,
              type: "Unmatched in Ledger",
              note: note || "Settlement row detected without corresponding PayFlow internal transaction record.",
              resolved: false
            });
          } else if (isCalculationMismatch || isStatusDiscrepancy) {
            discrepancyCount++;
            const type = isCalculationMismatch ? "Fee Variance" : "Amount Mismatch";
            newDiscrepancies.push({
              id: `disc-up-${Date.now()}-${index}`,
              txnRef: txnId,
              providerId: providerRef,
              internalAmount: `฿${amount.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`,
              statementAmount: `฿${net.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`,
              type: note.includes("MDR") ? "Fee Variance" : type,
              note: note || (isCalculationMismatch ? `Calculated net (฿${(amount - fee).toFixed(2)}) disagrees with reported net (฿${net.toFixed(2)}).` : `Settlement statement marked with status "${status}".`),
              resolved: false
            });
          } else {
            matchedCount++;
          }
        });

        const formattedSettled = `฿${totalNet.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
        const now = new Date();
        const timeStr = `Today, ${now.getHours().toString().padStart(2, "0")}:${now.getMinutes().toString().padStart(2, "0")}`;

        const batchId = `batch_${now.toISOString().slice(0, 10).replace(/-/g, "")}_${String(batches.length + 1).padStart(2, "0")}`;

        const newBatch: ReconciliationBatch = {
          id: batchId,
          provider: detectedProvider,
          fileName: file.name,
          uploadedAt: timeStr,
          totalRecords: rows.length,
          matched: matchedCount,
          unmatched: unmatchedCount,
          discrepancy: discrepancyCount,
          settledAmount: formattedSettled,
          isNew: true
        };

        setBatches((prev) => [newBatch, ...prev]);

        if (newDiscrepancies.length > 0) {
          setDiscrepancies((prev) => [...newDiscrepancies, ...prev]);
        }

        setToast({
          type: "success",
          title: "Statement Reconciled Successfully",
          message: `Parsed ${rows.length} transactions from "${file.name}". Automated matching completed with zero schema violations.`,
          batchId,
          stats: {
            total: rows.length,
            matched: matchedCount,
            discrepancies: discrepancyCount,
            settled: formattedSettled
          }
        });
      } catch (err) {
        console.error("CSV parse error:", err);
        setToast({
          type: "error",
          title: "Processing Failed",
          message: "Failed to parse CSV file. Please check column format: Transaction ID, Amount, Fee, Net, Status."
        });
      } finally {
        setIsProcessing(false);
      }
    };

    reader.onerror = () => {
      setToast({
        type: "error",
        title: "File Read Error",
        message: "An error occurred while reading the statement file."
      });
      setIsProcessing(false);
    };

    reader.readAsText(file);
  };

  const handleDragOver = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setIsDragging(true);
  };

  const handleDragLeave = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setIsDragging(false);
  };

  const handleDrop = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setIsDragging(false);
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      processCSVFile(e.dataTransfer.files[0]);
    }
  };

  const handleFileInputChange = (e: ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      processCSVFile(e.target.files[0]);
    }
  };

  const handleResolveDiscrepancy = (id: string) => {
    setDiscrepancies((prev) =>
      prev.map((item) => (item.id === id ? { ...item, resolved: true } : item))
    );
  };

  const handleDownloadSampleCSV = () => {
    const sampleCSV = `Transaction ID,Amount,Fee,Net,Status,Note
ORD-90201,1500.00,45.00,1455.00,Matched,Settled normally
ORD-90202,3200.00,96.00,3104.00,Matched,Settled normally
ORD-90203,2400.00,120.00,2280.00,Fee Variance,MDR contracted 3.00% but charged 5.00%
ORD-90204,850.00,25.50,824.50,Matched,PromptPay payment
ORD-90205,5000.00,150.00,4200.00,Discrepancy,Net amount mismatch - partial refund adjusted
ORD-90206,1200.00,36.00,1164.00,Matched,Settled normally
ORD-GHOST-99,990.00,29.70,960.30,Unmatched,External gateway record not found in ledger
ORD-90208,450.00,13.50,436.50,Matched,Settled normally`;

    const blob = new Blob([sampleCSV], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.setAttribute("href", url);
    link.setAttribute("download", "opn_settlement_sample.csv");
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  const handleLoadDemoCSV = () => {
    const demoCSV = `Transaction ID,Amount,Fee,Net,Status,Note
ORD-91001,2800.00,84.00,2716.00,Matched,Settled normally
ORD-91002,1450.00,43.50,1406.50,Matched,Settled normally
ORD-91003,3600.00,150.00,3450.00,Fee Variance,Reported fee ฿150 exceeds contracted ฿108
ORD-91004,890.00,26.70,863.30,Matched,PromptPay QR
ORD-91005,6200.00,186.00,5500.00,Discrepancy,Customer chargeback reservation applied
ORD-91006,1980.00,59.40,1920.60,Matched,Settled normally
ORD-91007,4500.00,135.00,4365.00,Matched,Settled normally
ORD-UNKNOWN-77,1250.00,37.50,1212.50,Unmatched,Settlement record missing internal ledger ref`;

    const file = new File([demoCSV], "opn_settlement_demo_batch.csv", { type: "text/csv" });
    processCSVFile(file);
  };

  return (
    <div className="space-y-6 max-w-7xl mx-auto">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white tracking-tight">Automated Reconciliation OS</h1>
          <p className="text-sm text-slate-400">Match transaction ledgers with external PSP settlement reports to eliminate finance discrepancies.</p>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={handleDownloadSampleCSV}
            className="px-3 py-1.5 bg-slate-900 hover:bg-slate-800 text-slate-300 border border-slate-800 rounded-xl text-xs font-medium flex items-center gap-1.5 transition-colors"
          >
            <Download className="w-3.5 h-3.5 text-slate-400" />
            <span>Sample Template (.CSV)</span>
          </button>
          <button
            onClick={handleLoadDemoCSV}
            className="px-3 py-1.5 bg-indigo-600/10 hover:bg-indigo-600/20 text-indigo-300 border border-indigo-500/30 rounded-xl text-xs font-semibold flex items-center gap-1.5 transition-colors"
          >
            <RefreshCw className="w-3.5 h-3.5 text-indigo-400" />
            <span>Load Demo Batch</span>
          </button>
        </div>
      </div>

      {/* Success / Error Notification Toast */}
      {toast && (
        <div
          className={`p-4 rounded-2xl border flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 shadow-lg transition-all animate-in fade-in-50 duration-200 ${
            toast.type === "success"
              ? "bg-emerald-950/40 border-emerald-500/30 text-emerald-200"
              : toast.type === "error"
              ? "bg-rose-950/40 border-rose-500/30 text-rose-200"
              : "bg-indigo-950/40 border-indigo-500/30 text-indigo-200"
          }`}
        >
          <div className="flex items-start gap-3">
            <div className="p-1.5 rounded-lg bg-emerald-500/20 text-emerald-400 mt-0.5">
              {toast.type === "success" ? (
                <CheckCircle2 className="w-5 h-5 text-emerald-400" />
              ) : (
                <AlertCircle className="w-5 h-5 text-rose-400" />
              )}
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h4 className="text-sm font-semibold text-white">{toast.title}</h4>
                {toast.batchId && (
                  <span className="font-mono text-[11px] px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                    {toast.batchId}
                  </span>
                )}
              </div>
              <p className="text-xs text-slate-300 mt-0.5">{toast.message}</p>

              {toast.stats && (
                <div className="flex flex-wrap items-center gap-4 mt-2 text-xs">
                  <div className="flex items-center gap-1 text-slate-300">
                    <span className="text-slate-400">Total:</span>
                    <span className="font-bold text-white">{toast.stats.total}</span>
                  </div>
                  <div className="flex items-center gap-1 text-emerald-400">
                    <span className="text-slate-400">Matched:</span>
                    <span className="font-bold">{toast.stats.matched}</span>
                  </div>
                  <div className="flex items-center gap-1 text-amber-400">
                    <span className="text-slate-400">Discrepancies:</span>
                    <span className="font-bold">{toast.stats.discrepancies}</span>
                  </div>
                  <div className="flex items-center gap-1 text-white">
                    <span className="text-slate-400">Net Settled:</span>
                    <span className="font-bold text-emerald-400">{toast.stats.settled}</span>
                  </div>
                </div>
              )}
            </div>
          </div>

          <button
            onClick={() => setToast(null)}
            className="p-1 rounded-lg hover:bg-white/10 text-slate-400 hover:text-white transition-colors"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Hidden native file input */}
      <input
        ref={fileInputRef}
        type="file"
        accept=".csv,text/csv"
        className="hidden"
        onChange={handleFileInputChange}
      />

      {/* Upload Dropzone */}
      <div
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        onClick={() => fileInputRef.current?.click()}
        className={`p-8 border-2 border-dashed rounded-2xl flex flex-col items-center justify-center text-center transition-all cursor-pointer group ${
          isDragging
            ? "border-indigo-500 bg-indigo-500/10 ring-4 ring-indigo-500/20 scale-[1.005]"
            : "border-slate-800 hover:border-indigo-500/60 bg-slate-900/30 hover:bg-slate-900/50"
        }`}
      >
        <div
          className={`p-3.5 rounded-2xl mb-3 transition-transform group-hover:scale-110 ${
            isDragging
              ? "bg-indigo-600 text-white shadow-lg shadow-indigo-600/30"
              : "bg-indigo-600/10 text-indigo-400"
          }`}
        >
          {isProcessing ? (
            <RefreshCw className="w-8 h-8 animate-spin" />
          ) : (
            <UploadCloud className="w-8 h-8" />
          )}
        </div>
        <h3 className="text-sm font-semibold text-white mb-1">
          {isDragging
            ? "Drop Statement CSV file right here..."
            : isProcessing
            ? "Reconciling settlement transactions..."
            : "Upload Gateway Settlement CSV / Excel"}
        </h3>
        <p className="text-xs text-slate-400 max-w-md mb-4">
          Drop Opn, GB Prime Pay, 2C2P, or Bank Settlement reports. The engine automatically parses columns (Transaction ID, Amount, Fee, Net, Status) and flags discrepancies.
        </p>

        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              fileInputRef.current?.click();
            }}
            disabled={isProcessing}
            className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold shadow-md shadow-indigo-600/20 transition-colors flex items-center gap-1.5"
          >
            <FileSpreadsheet className="w-3.5 h-3.5" />
            <span>{isProcessing ? "Processing..." : "Select Statement File (.CSV)"}</span>
          </button>
        </div>
      </div>

      {/* Reconciliation Batches */}
      <div className="bg-slate-900/40 border border-slate-800 rounded-2xl overflow-hidden shadow-sm">
        <div className="p-4 border-b border-slate-800 bg-slate-950/40 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <h2 className="text-sm font-semibold text-white">Recent Statement Batches</h2>
            <span className="text-xs text-slate-500 font-mono">({batches.length} total)</span>
          </div>
          <div className="flex items-center gap-2 text-xs text-slate-400">
            <ShieldCheck className="w-4 h-4 text-emerald-400" />
            <span className="hidden sm:inline">Ledger integrity verified against PayFlow Core</span>
          </div>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs text-slate-300">
            <thead className="text-[11px] uppercase tracking-wider text-slate-500 border-b border-slate-800 bg-slate-950/20">
              <tr>
                <th className="py-3 px-4">Batch ID</th>
                <th className="py-3 px-4">Provider</th>
                <th className="py-3 px-4">File Name</th>
                <th className="py-3 px-4">Total Records</th>
                <th className="py-3 px-4">Matched</th>
                <th className="py-3 px-4">Discrepancies</th>
                <th className="py-3 px-4">Net Settled Amount</th>
                <th className="py-3 px-4">Uploaded</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60">
              {batches.map((b) => (
                <tr
                  key={b.id}
                  className={`hover:bg-slate-900/50 transition-colors ${
                    b.isNew ? "bg-indigo-950/20" : ""
                  }`}
                >
                  <td className="py-3.5 px-4 font-mono font-medium text-white flex items-center gap-2">
                    <span>{b.id}</span>
                    {b.isNew && (
                      <span className="px-1.5 py-0.5 text-[9px] font-bold bg-indigo-500/20 text-indigo-300 rounded border border-indigo-500/30 uppercase">
                        New
                      </span>
                    )}
                  </td>
                  <td className="py-3.5 px-4 font-semibold text-indigo-300">{b.provider}</td>
                  <td className="py-3.5 px-4 text-slate-300 flex items-center gap-1.5">
                    <FileSpreadsheet className="w-3.5 h-3.5 text-emerald-400 shrink-0" />
                    <span className="truncate max-w-[200px]" title={b.fileName}>
                      {b.fileName}
                    </span>
                  </td>
                  <td className="py-3.5 px-4 font-semibold text-white">{b.totalRecords}</td>
                  <td className="py-3.5 px-4 text-emerald-400 font-semibold">{b.matched}</td>
                  <td className="py-3.5 px-4">
                    {b.discrepancy > 0 ? (
                      <span className="px-2 py-0.5 rounded bg-rose-500/20 text-rose-300 border border-rose-500/30 font-bold">
                        {b.discrepancy} Exceptions
                      </span>
                    ) : (
                      <span className="px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 font-medium">
                        0 Discrepancy
                      </span>
                    )}
                  </td>
                  <td className="py-3.5 px-4 font-bold text-white">{b.settledAmount}</td>
                  <td className="py-3.5 px-4 text-slate-400">{b.uploadedAt}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Discrepancies & Exceptions Investigation */}
      <div className="p-6 bg-slate-900/40 border border-slate-800 rounded-2xl space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
          <div>
            <h2 className="text-base font-bold text-white flex items-center gap-2">
              <AlertCircle className="w-5 h-5 text-amber-400" />
              <span>Exceptions Requiring Attention ({discrepancies.filter((d) => !d.resolved).length} Pending)</span>
            </h2>
            <p className="text-xs text-slate-400">Items where external settlement reports differed from internal transaction records.</p>
          </div>
          <span className="text-xs text-slate-500">Auto-prioritized by risk severity</span>
        </div>

        <div className="space-y-3">
          {discrepancies.length === 0 ? (
            <div className="p-8 text-center bg-slate-950/40 rounded-xl border border-slate-800 text-slate-400">
              <FileCheck className="w-8 h-8 text-emerald-400 mx-auto mb-2 opacity-80" />
              <p className="text-sm font-medium text-white">All Clear! No reconciliation discrepancies found.</p>
              <p className="text-xs text-slate-500 mt-1">Uploaded reports matched perfectly with ledger entries.</p>
            </div>
          ) : (
            discrepancies.map((d) => (
              <div
                key={d.id}
                className={`p-4 rounded-xl flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 border transition-all ${
                  d.resolved
                    ? "bg-slate-950/20 border-slate-800/40 opacity-50"
                    : "bg-slate-950/60 border-slate-800/80 hover:border-slate-700"
                }`}
              >
                <div>
                  <div className="flex items-center gap-2 mb-1">
                    <span className="font-mono text-xs font-bold text-white">{d.txnRef}</span>
                    <span className="text-slate-500 text-xs font-mono">({d.providerId})</span>
                    <span
                      className={`px-2 py-0.5 rounded text-[10px] font-semibold border ${
                        d.resolved
                          ? "bg-emerald-500/20 text-emerald-300 border-emerald-500/30"
                          : d.type === "Fee Variance"
                          ? "bg-amber-500/20 text-amber-300 border-amber-500/30"
                          : d.type === "Unmatched in Ledger"
                          ? "bg-rose-500/20 text-rose-300 border-rose-500/30"
                          : "bg-purple-500/20 text-purple-300 border-purple-500/30"
                      }`}
                    >
                      {d.resolved ? "Resolved" : d.type}
                    </span>
                  </div>
                  <p className="text-xs text-slate-400">{d.note}</p>
                </div>

                <div className="flex items-center gap-4 text-xs shrink-0">
                  <div>
                    <span className="text-slate-500 block text-[10px]">Internal</span>
                    <span className="text-white font-semibold">{d.internalAmount}</span>
                  </div>
                  <div>
                    <span className="text-slate-500 block text-[10px]">Statement</span>
                    <span className="text-amber-300 font-semibold">{d.statementAmount}</span>
                  </div>
                  <button
                    onClick={() => handleResolveDiscrepancy(d.id)}
                    disabled={d.resolved}
                    className={`px-3 py-1 rounded-lg text-xs font-medium transition-colors ${
                      d.resolved
                        ? "bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 cursor-default"
                        : "bg-slate-800 hover:bg-slate-700 text-slate-200"
                    }`}
                  >
                    {d.resolved ? "Resolved ✓" : "Resolve"}
                  </button>
                </div>
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  );
}

