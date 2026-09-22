import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { chromium } from 'playwright';

const script = await readFile(new URL('../../Client.Browser/wwwroot/run-recordings.js', import.meta.url));
const server = createServer((request, response) => {
    response.setHeader('Content-Type', request.url === '/recordings.js' ? 'text/javascript' : 'text/html');
    response.end(request.url === '/recordings.js' ? script : '<script src="/recordings.js"></script>');
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const browser = await chromium.launch({ headless: true });
try {
    const page = await browser.newPage();
    await page.goto(`http://127.0.0.1:${server.address().port}`);
    await page.evaluate(async () => {
        await OpenGarrisonRunRecordings.save('proof', JSON.stringify({ Id: 'proof', Recording: 'AQID' }));
        await OpenGarrisonRunRecordings.save('claim', JSON.stringify({ Id: 'claim', AttemptId: 'run' }));
    });
    await page.reload();
    let records = JSON.parse(await page.evaluate(() => OpenGarrisonRunRecordings.load()));
    assert.equal(records.length, 2);
    assert.equal(records.find(record => record.Id === 'proof').Recording, 'AQID');
    await page.evaluate(async () => {
        await OpenGarrisonRunRecordings.save('proof', JSON.stringify({ Id: 'proof', JobId: 'accepted' }));
        await OpenGarrisonRunRecordings.remove('claim');
    });
    await page.reload();
    records = JSON.parse(await page.evaluate(() => OpenGarrisonRunRecordings.load()));
    assert.deepEqual(records, [{ Id: 'proof', JobId: 'accepted' }]);
    await page.evaluate(() => OpenGarrisonRunRecordings.remove('proof'));
    assert.deepEqual(JSON.parse(await page.evaluate(() => OpenGarrisonRunRecordings.load())), []);
    console.log('Run recording storage: save, reload, update and delete passed.');
} finally {
    await browser.close();
    await new Promise(resolve => server.close(resolve));
}
