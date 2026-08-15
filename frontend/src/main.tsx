import React from 'react'
import ReactDOM from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { App, DashboardPage, TransactionsPage, ManualTransactionPage, EditTransactionPage, TransactionDetailPage, UploadPage } from './App'
import './styles/global.css'

const router = createBrowserRouter([{ path: '/', element: <App />, children: [
  { index: true, element: <DashboardPage /> }, { path: 'transactions', element: <TransactionsPage /> },
  { path: 'transactions/new', element: <ManualTransactionPage /> }, { path: 'transactions/:id/edit', element: <EditTransactionPage /> }, { path: 'transactions/:id', element: <TransactionDetailPage /> }, { path: 'upload', element: <UploadPage /> },
  { path: '*', element: <main className="page"><h1>Page not found</h1><a className="button" href="/">Return to dashboard</a></main> }
]}])
ReactDOM.createRoot(document.getElementById('root')!).render(<React.StrictMode><RouterProvider router={router} /></React.StrictMode>)
