"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { 
  LayoutDashboard, 
  ArrowLeftRight, 
  GitFork, 
  FileCheck2, 
  Code2, 
  Search, 
  Zap, 
  X,
  ExternalLink,
  ShieldCheck
} from "lucide-react";

interface CommandPaletteProps {
  isOpen: boolean;
  onClose: () => void;
}

export function CommandPalette({ isOpen, onClose }: CommandPaletteProps) {
  const router = useRouter();
  const [search, setSearch] = useState("");

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        onClose();
      }
    };
    if (isOpen) {
      window.addEventListener("keydown", handleKeyDown);
    }
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  const actions = [
    { name: "Dashboard Overview", category: "Navigation", href: "/", icon: LayoutDashboard },
    { name: "Live Transactions & Failovers", category: "Navigation", href: "/transactions", icon: ArrowLeftRight },
    { name: "Smart Routing Rules Configurator", category: "Navigation", href: "/routing-rules", icon: GitFork },
    { name: "Automated Reconciliation Center", category: "Navigation", href: "/reconciliation", icon: FileCheck2 },
    { name: "Interactive API Reference & OpenAPI Spec", category: "Developer", href: "/api-docs", icon: Code2 },
    { name: "API Keys & Webhook Test Console", category: "Developer", href: "/developers", icon: ShieldCheck },
  ];

  const filtered = actions.filter((a) =>
    a.name.toLowerCase().includes(search.toLowerCase()) ||
    a.category.toLowerCase().includes(search.toLowerCase())
  );

  const handleSelect = (action: typeof actions[0]) => {
    onClose();
    if (action.href.startsWith("http")) {
      window.open(action.href, "_blank");
    } else {
      router.push(action.href);
    }
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-md flex items-start justify-center pt-24 p-4 animate-in fade-in duration-150">
      <div className="bg-[#0D121F] border border-white/[0.1] rounded-2xl w-full max-w-xl shadow-2xl overflow-hidden shadow-black/80 flex flex-col">
        {/* Search Input */}
        <div className="flex items-center gap-3 px-4 py-3.5 border-b border-white/[0.08] bg-slate-900/40">
          <Search className="w-4 h-4 text-indigo-400" />
          <input
            type="text"
            autoFocus
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Type a command, page, or search reference..."
            className="w-full bg-transparent text-sm text-white placeholder-slate-500 focus:outline-none"
          />
          <button
            onClick={onClose}
            className="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-slate-800/60"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Action List */}
        <div className="p-2 max-h-80 overflow-y-auto divide-y divide-white/[0.04]">
          <div className="px-3 py-1.5 text-[10px] font-semibold tracking-wider uppercase text-slate-500">
            Quick Actions & Pages
          </div>
          {filtered.length === 0 ? (
            <div className="py-8 text-center text-xs text-slate-500">
              No results found for &quot;{search}&quot;
            </div>
          ) : (
            filtered.map((item, idx) => {
              const Icon = item.icon;
              return (
                <button
                  key={idx}
                  onClick={() => handleSelect(item)}
                  className="w-full flex items-center justify-between px-3 py-2.5 rounded-xl hover:bg-indigo-600/10 hover:border-indigo-500/20 text-left transition-colors group"
                >
                  <div className="flex items-center gap-3">
                    <div className="p-2 rounded-lg bg-slate-900 border border-white/[0.06] text-slate-400 group-hover:text-indigo-400 group-hover:border-indigo-500/30 transition-colors">
                      <Icon className="w-4 h-4" />
                    </div>
                    <div>
                      <div className="text-xs font-semibold text-white group-hover:text-indigo-200">{item.name}</div>
                      <div className="text-[10px] text-slate-400">{item.category}</div>
                    </div>
                  </div>
                  <span className="text-[10px] font-mono text-slate-500 group-hover:text-indigo-400">
                    Jump &rarr;
                  </span>
                </button>
              );
            })
          )}
        </div>

        {/* Footer info */}
        <div className="px-4 py-2.5 bg-slate-950/60 border-t border-white/[0.06] flex items-center justify-between text-[11px] text-slate-400">
          <span>Navigate with mouse or keyboard</span>
          <span className="font-mono text-[10px] bg-slate-900 px-1.5 py-0.5 rounded border border-white/[0.08]">
            ESC to close
          </span>
        </div>
      </div>
    </div>
  );
}
