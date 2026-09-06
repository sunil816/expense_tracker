export type Direction = 'Debit' | 'Credit'
export type TransactionKind = 'Expense' | 'Income' | 'Transfer'
export type Origin = 'Imported' | 'Manual'
export type SourceFormat = 'PaymentExport' | 'BankStatement' | 'OrderHistory'
export type CategoryKind = 'Income' | 'Expense'
export type Granularity = 'Week' | 'Month'
export type LineExtractionStatus = 'NotApplicable' | 'Unavailable' | 'Available'
export type TagState = 'Suggested' | 'Confirmed' | 'Rejected'
export type TagSource = 'Rule' | 'Manual'
export type SourceProvider = 'SuperMoney' | 'Instamart' | 'BankStatement'
export type DuplicateMatchReason = 'ExternalReference' | 'Composite'
export type DuplicateFlagState = 'Suggested' | 'Confirmed' | 'Rejected'
export type DuplicateFlagDecision = Exclude<DuplicateFlagState, 'Suggested'>
export type TransactionLineType = 'Product' | 'Tax' | 'Fee' | 'Discount' | 'Other'
export interface Category { id: string; slug: string; name: string; kind: CategoryKind; parentCategoryId: string | null }
export interface Tag { id: string; slug: string; name: string }
export interface TransactionSummary { id: string; origin: Origin; transactionDate: string; description: string; note: string | null; accountLabel: string | null; externalReference: string | null; direction: Direction; kind: TransactionKind; amount: number; currency: string; categoryId: string | null; receiptUrl: string | null; lineExtractionStatus: LineExtractionStatus; hasLines: boolean }
export interface TransactionPage { items: TransactionSummary[]; page: number; pageSize: number; totalCount: number }
export interface BulkCategoryResponse { updatedCount: number }
export interface TransactionTag { tagId: string; slug: string; name: string; state: TagState; source: TagSource; decidedAt: string }
export interface TransactionLine { id: string; position: number; lineType: TransactionLineType; description: string; quantity: number | null; unitAmount: number | null; amount: number; tags: TransactionTag[] }
export interface TransactionDetail extends Omit<TransactionSummary, 'hasLines' | 'origin'> { origin: Origin; documentImportId: string | null; importPosition: number | null; sourceFormat: SourceFormat | null; balanceAfter: number | null; lines: TransactionLine[]; tags: TransactionTag[] }
export interface ManualTransactionCreated { id: string; transactionDate: string; description: string; note: string | null; accountLabel: string | null; externalReference: string | null; direction: Direction; kind: TransactionKind; amount: number; currency: string; categoryId: string | null; receiptUrl: string | null }
export interface TransactionWrite { transactionDate: string; description: string; note?: string | null; direction: Direction; kind?: TransactionKind; amount: number; accountLabel?: string | null; externalReference?: string | null; categoryId?: string | null; receiptUrl?: string | null }
export interface SpendingRow { periodStart: string; categoryId: string | null; categoryName: string | null; categoryKind: CategoryKind | null; direction: Direction; total: number; count: number }
export interface SpendingReport { granularity: Granularity; from: string; to: string; rows: SpendingRow[] }
export interface DuplicateFlagDetails { flagId: string; matchedTransactionId: string; reason: DuplicateMatchReason; state: DuplicateFlagState; suggestedAt: string; decidedAt: string | null }
export interface DuplicateReviewQuery { state?: DuplicateFlagState; page?: number; pageSize?: number }
export interface DuplicateReviewTransaction { id: string; origin: Origin; transactionDate: string; description: string; accountLabel: string | null; externalReference: string | null; direction: Direction; kind: TransactionKind; amount: number; currency: string; sourceFormat: SourceFormat | null }
export interface DuplicateReviewItem { flag: DuplicateFlagDetails & { transactionId: string }; flaggedTransaction: DuplicateReviewTransaction; matchedTransaction: DuplicateReviewTransaction }
export interface DuplicateReviewPage { items: DuplicateReviewItem[]; page: number; pageSize: number; totalCount: number }
export interface SavedTransaction { id: string; importPosition: number; sourceSequence: number | null; sourceFormat: SourceFormat; transactionDate: string; description: string; accountLabel: string | null; externalReference: string | null; direction: Direction; kind: TransactionKind; amount: number; currency: string; balanceAfter: number | null; categoryId: string | null; receiptUrl: string | null; lineExtractionStatus: LineExtractionStatus; duplicateFlag: DuplicateFlagDetails | null }
export interface ImportResult { importId: string; isDuplicate: boolean; importedAt: string; provider: SourceProvider; importedCount: number; flaggedDuplicateCount: number; transactions: SavedTransaction[] }
export interface TransactionListParams { from?: string; to?: string; direction?: Direction; kind?: TransactionKind; categoryId?: string; uncategorized?: boolean; origin?: Origin; sourceFormat?: SourceFormat; page?: number; pageSize?: number }
