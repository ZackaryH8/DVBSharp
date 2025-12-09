"use client";

import { useState, type ReactNode } from "react";
import { Check, Copy } from "lucide-react";
import { Button, type ButtonProps } from "@/components/ui/button";
import { cn } from "@/lib/utils";

type CopyButtonProps = {
  value: string;
  className?: string;
  children?: ReactNode;
} & Pick<ButtonProps, "variant" | "size">;

export function CopyButton({
  value,
  className,
  children,
  variant = "outline",
  size = "icon",
}: CopyButtonProps) {
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch (err) {
      console.error("Failed to copy", err);
    }
  }

  const icon = copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />;

  return (
    <Button
      type="button"
      variant={variant}
      size={size}
      onClick={handleCopy}
      className={cn("gap-2", className)}
    >
      {children ? (
        <>
          {icon}
          <span>{copied ? "Copied" : children}</span>
        </>
      ) : (
        icon
      )}
    </Button>
  );
}
