

> [!NOTE]
> I will be creating my scenarios within the single Todos.feature file. Though I understand best practice would be splitting the features by area (ex: Filtering.feature, BulkActions.feature), I figured a single well organized feature file would be easier to review in this take home test scenario!

## Assumptions: 

- The application is always running locally at port 5173
- Chrome is the only target browser
- ASPNETCORE_ENVIRONMENT=Development is required
- ChromeDriver version must match the installed Chrome version

## Test Data Strategy

**E2E Tests**:

I'm going to go with a reset + seed data per scenario strategy here. Using shared static data would lead to tests becoming order-dependent and fragile, and obviously using the UI to create all of our data would just be slow, fragile, and too dependent on the UI working (if "add" is broken, _every_ test is now broken because we can't create our data).

Thus, I have added `Given I reset data` to the Background block in our feature file so it runs before every scenario. Then, each scenario will only seed what is needed via the `Given` steps.

**Service Tests**:

Each test instantiates a fresh `InMemoryTodoRepository` per test via `NewSvc()`. There is no shared state between tests, isolation is guaranteed by construction rather than cleanup.

## Improvements

**Loading indicators**:

The app provides no loading state indicators which made reliable post action waits a bit difficult with Selenium. I would push for data-loading attributes or similar signal to make tests deterministic without relying on timing heuristics. 

**Empty title gives no feedback**:

The user gets no feedback when they try to create a todo with an empty title. We can add an E2E scenario for an empty title once the UI surfaces and error message.

**Notes not visible in the list**:
	
The app doesn't render notes visibly in the list. I've added service tests for now, but E2E coverage would require notes be implemented and visible in the browser.

**Testing infrastructure improvement**:

Several of the originally provided steps relied on Wait.Until(_ => true) which passes immediately and provides no actual synchronization. Replacing these with explicit element or state checks would improve test stability.