import { Alert } from 'antd'
import type { ReactNode } from 'react'

/**
 * Inline info bar — port of Electron Controls/Custom/InfoBar.cs (Wpf.Ui InfoBar).
 * Severity maps to the Electron InfoBarSeverity values.
 */

export type InfoBarSeverity = 'informational' | 'success' | 'warning' | 'error'

export interface InfoBarProps {
  title?: string
  message?: ReactNode
  severity?: InfoBarSeverity
  closable?: boolean
  onClose?: () => void
  action?: ReactNode
  className?: string
}

const SEVERITY_TO_ALERT_TYPE: Record<InfoBarSeverity, 'info' | 'success' | 'warning' | 'error'> = {
  informational: 'info',
  success: 'success',
  warning: 'warning',
  error: 'error'
}

export default function InfoBar({
  title,
  message,
  severity = 'informational',
  closable = false,
  onClose,
  action,
  className
}: InfoBarProps): React.JSX.Element {
  return (
    <Alert
      className={`udt-info-bar udt-info-bar--${severity}${className ? ` ${className}` : ''}`}
      type={SEVERITY_TO_ALERT_TYPE[severity]}
      showIcon
      message={title}
      description={message}
      closable={closable}
      onClose={onClose}
      action={action}
    />
  )
}
