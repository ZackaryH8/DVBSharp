import Link from "next/link";
import { Button } from "./ui/button";
import { ThemeToggle } from "./theme-toggle";

const links = [
  { href: "/", label: "Dashboard" },
  { href: "/tuners", label: "Tuners" },
  { href: "/muxes", label: "Muxes" },
  { href: "/channels", label: "Channels" },
  { href: "/discovery", label: "Discovery" },
];

export function NavBar() {
  return (
    <header className="border-b bg-background/80 backdrop-blur">
      <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
        <Link href="/" className="text-lg font-semibold">
          DVBSharp UI
        </Link>
        <nav className="flex items-center gap-2">
          {links.map((link) => (
            <Button key={link.href} variant="ghost" asChild>
              <Link href={link.href}>{link.label}</Link>
            </Button>
          ))}
          <ThemeToggle />
        </nav>
      </div>
    </header>
  );
}
